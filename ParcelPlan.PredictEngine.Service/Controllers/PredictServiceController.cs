using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using ParcelPlan.Common;
using ParcelPlan.Common.MassTransit.Contracts;
using ParcelPlan.PredictEngine.Service.Entities;
using ParcelPlan.PredictEngine.Service.Models;
using static MassTransit.ValidationResultExtensions;
using static ParcelPlan.PredictEngine.Service.Dtos;

namespace ParcelPlan.PredictEngine.Service.Controllers
{
    [ApiController]
    [Route("predict/service")]
    public class PredictServiceController : ControllerBase
    {
        private readonly HttpClient predictEngineClient;
        private readonly IConfiguration configuration;
        private readonly IRepository<LocaleDataEntity> localeRepository;
        private readonly IRepository<AS_LocaleDataEntity> as_localeDataRepository;
        private readonly IRepository<SpecialLocaleEntity> specialLocaleRepository;
        private readonly IRepository<Log> logRepository;
        private readonly IRequestClient<PredictEngineRateRequestCreated> rateEngineRequestClient;
        private readonly PredictCostController predictorCostController;
        private readonly IBus iBus;
        private readonly IOptions<ApiBehaviorOptions> apiBehaviorOptions;

        public PredictServiceController(HttpClient predictEngineClient, IConfiguration configuration,
            IRepository<LocaleDataEntity> localeRepository, IRepository<AS_LocaleDataEntity> as_localeDataRepository, 
            IRepository<SpecialLocaleEntity> specialLocaleRepository, IRepository<Log> logRepository, 
            IRequestClient<PredictEngineRateRequestCreated> rateEngineRequestClient, PredictCostController predictorCostController, 
            IBus iBus, IOptions<ApiBehaviorOptions> apiBehaviorOptions)
        {
            this.predictEngineClient = predictEngineClient;
            this.configuration = configuration;
            this.localeRepository = localeRepository;
            this.as_localeDataRepository = as_localeDataRepository;
            this.specialLocaleRepository = specialLocaleRepository;
            this.logRepository = logRepository;
            this.rateEngineRequestClient = rateEngineRequestClient;
            this.predictorCostController = predictorCostController;
            this.iBus = iBus;
            this.apiBehaviorOptions = apiBehaviorOptions;         
        }

        [HttpPost]
        public async Task<ActionResult<PredictServiceResultDto>> PostAsync(PredictServiceRequestDto predictServiceRequestDto)
        {

            if (string.IsNullOrEmpty(predictServiceRequestDto.RateGroup))
            {
                ModelState.AddModelError(nameof(PredictServiceResultDto), $"Please provide a valid rate group in your request.");

                await LogMessageAsync(Level.ERROR, "The prediction engine request was not valid (Bad Request).  Rate group missing.");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            var predictRequest = await CreatePredictServiceRequestObjectAsync(predictServiceRequestDto, predictServiceRequestDto.RateGroup);

            if (predictRequest == null)
            {
                ModelState.AddModelError(nameof(ServicePredictionUnit), $"Please provide a valid request.");

                await LogMessageAsync(Level.ERROR, "The prediction engine request was not valid (Bad Request).");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            predictRequest.PostalCodePrefix = predictRequest.PostalCode.ToString().Substring(0, 3);

            var context = new MLContext();

            var serviceModelFilePath = this.configuration.GetValue<string>("ModelFiles:ServiceModelPath");

            if (!Directory.Exists(serviceModelFilePath))
            {
                ModelState.AddModelError(nameof(ServicePredictionUnit), $"Model file path could not be found: {serviceModelFilePath}");

                await LogMessageAsync(Level.ERROR, $"Model file path could not be found: {serviceModelFilePath}");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            if (!System.IO.File.Exists($"{serviceModelFilePath}{predictRequest.RateGroup}.zip"))
            {
                ModelState.AddModelError(nameof(ServicePredictionUnit), $"Model file could not be found: {serviceModelFilePath}{predictRequest.RateGroup}.zip");

                await LogMessageAsync(Level.ERROR, $"Model file could not be found: {serviceModelFilePath}{predictRequest.RateGroup}.zip");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            var trainedModel = context.Model.Load($"{serviceModelFilePath}{predictRequest.RateGroup}.zip", out DataViewSchema modelSchema);

            if (trainedModel == null)
            {
                ModelState.AddModelError(nameof(ServicePredictionUnit), $"Model file could not be found: {serviceModelFilePath}{predictRequest.RateGroup}.zip");

                await LogMessageAsync(Level.ERROR, $"Model file could not be found: {serviceModelFilePath}{predictRequest.RateGroup}.zip");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            PredictServiceResult prediction = new();

            try
            {
                var predictEngine = context.Model.CreatePredictionEngine<ServicePredictionUnit, PredictServiceResult>(trainedModel);

                prediction = predictEngine.Predict(predictRequest);
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(nameof(ServicePredictionUnit), $"Model Error:  Invalid service prediction model.  " +
                    $"Please ensure that you are using a valid service prediction model.");

                await LogMessageAsync(Level.ERROR, $"Model Error:  Invalid service prediction model.  " +
                    $"Please ensure that you are using a valid service prediction model.");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }


            var predictionResult = new PredictServiceResultDto();

            double confidence = 0;

            if (prediction != null)
            {
                predictionResult.PredictedService = prediction.PredictedLabelString;

                var entropy = -prediction.Score.Sum(p => p * Math.Log(p));

                confidence = 100 - (entropy * 100);

                // confidence = prediction.Score.Max() * 100;

                predictionResult.Confidence = $"{(confidence).ToString("0.00")}%";
            }


            var rateSpecialLocale = configuration.GetValue<bool>("RateSpecialLocale");

            var isLocale = (await localeRepository
                .GetAsync(x => x.PostalCode.Equals(predictServiceRequestDto.Receiver.Address.PostalCode))) != null;

            var isSpecialLocale = (await specialLocaleRepository
                .GetAsync(x => x.SpecialPostalCode.PostalCode.Equals(predictServiceRequestDto.Receiver.Address.PostalCode))) != null;


            if ((confidence < Convert.ToDouble(configuration.GetValue<string>("ConfidenceThreshold")) && isLocale) 
                || (isLocale && isSpecialLocale && rateSpecialLocale))
            {
                var rateEngineRequest = CreateRateEngineRequestObject(predictServiceRequestDto);

                var rateEngineResponse = await rateEngineRequestClient.GetResponse<PredictEngineRateResultCreated>(new PredictEngineRateRequestCreated(rateEngineRequest));

                var rateResult = rateEngineResponse.Message;

                if (rateResult.CarrierServiceName == null)
                {
                    predictionResult.PredictedService = "NoService";
                    predictionResult.Status.Code = 0;
                    predictionResult.Status.Description = "No qualifying carrier service is available for this shipment.";
                }
                else
                {
                    predictionResult.PredictedService = rateResult.CarrierServiceName;
                }

                predictionResult.Confidence = "100%";

                predictionResult.CarrierRated = true;

                predictionResult.Detail.EstimatedCost = rateResult.TotalCost;

                // predictionResult.Detail.EstimatedTransitDays = rateResult.Commit.TransitDays;

                if (isLocale)
                {
                    await iBus.Publish(rateResult.AsDto());
                }
            }
            else
            {
                var estimateCost = predictServiceRequestDto.EstimateCost == true ? true : false;

                var estimateTransitDays = predictServiceRequestDto.EstimateTransitDays == true ? true : false;


                if ((estimateCost) && isLocale && predictionResult.PredictedService != "NoService")
                {
                    var predictCostRequestDto = CreatePredictCostRequestObject(predictServiceRequestDto, predictionResult.PredictedService);

                    var predictCostResultResponse = predictorCostController.PostAsync(predictCostRequestDto).Result.Result;

                    if (predictCostResultResponse is not null && predictCostResultResponse is OkObjectResult okObjectResult)
                    {
                        decimal predictedCost = 0;

                        if (okObjectResult.Value is PredictCostResultDto predictCostResultDto)
                        {
                            predictedCost = Math.Round((decimal)predictCostResultDto.PredictedCost, 2);
                        }

                        predictionResult.Detail.EstimatedCost = Convert.ToDecimal(predictedCost);
                    }
                }
            }

            if (predictionResult.PredictedService == "NoService")
            {
                predictionResult.PredictedService = "None";
                predictionResult.Status.Code = 0;
                predictionResult.Status.Description = "No qualifying carrier service is available for this shipment.";
            }
            else if (!isLocale)
            {
                predictionResult.PredictedService = "None";
                predictionResult.Status.Code = 0;
                predictionResult.Confidence = "100";
                predictionResult.Status.Description = "The receiver postal code is not a valid United States postal code.";
            }
            else
            {
                predictionResult.Status.Code = 1;
                predictionResult.Status.Description = "Success";
            }

            return Ok(predictionResult);
        }

        public async Task<ServicePredictionUnit> CreatePredictServiceRequestObjectAsync(PredictServiceRequestDto predictRequestDto, string rateGroup)
        {
            var predictRequest = new ServicePredictionUnit
            {
                RateGroup = rateGroup,
                PostalCode = predictRequestDto.Receiver.Address.PostalCode,
                Residential = predictRequestDto.Receiver.Address.Residential.ToString()
            };

            float ratedWeight = 0;

            foreach (var package in predictRequestDto.Packages)
            {
                ratedWeight += (float)package.Weight.Value;
                predictRequest.SignatureRequired = package.SignatureRequired.ToString();
                predictRequest.AdultSignatureRequired = package.AdultSignatureRequired.ToString();
            }

            predictRequest.RatedWeight = ratedWeight;

            predictRequest.ShipDay = predictRequestDto.ShipDate.ToString("ddd").ToUpper();


            var as_LocaleEntity = (await as_localeDataRepository.GetAllAsync()).ToList();

            var as_locale = as_LocaleEntity.Where(x => x.PostalCode.Equals(predictRequest.PostalCode)).FirstOrDefault();

            var as_ChargeCode = AreaSurchargeChargeCodes.NONE;

            if (as_locale == null)
            {
                predictRequest.AreaSurchargesNone = "true";
                predictRequest.AreaSurchargesDelivery = "false";
                predictRequest.AreaSurchargesExtended = "false";
                predictRequest.AreaSurchargesRemote = "false";
            }
            else
            {
                as_ChargeCode = Enum.TryParse(as_locale.ChargeCode, out as_ChargeCode)
                    ? as_ChargeCode
                    : AreaSurchargeChargeCodes.NONE;

                switch (as_ChargeCode)
                {
                    case AreaSurchargeChargeCodes.DAS:
                        predictRequest.AreaSurchargesNone = "false";
                        predictRequest.AreaSurchargesDelivery = "true";
                        predictRequest.AreaSurchargesExtended = "false";
                        predictRequest.AreaSurchargesRemote = "false";
                        break;

                    case AreaSurchargeChargeCodes.EDAS:
                        predictRequest.AreaSurchargesNone = "false";
                        predictRequest.AreaSurchargesDelivery = "false";
                        predictRequest.AreaSurchargesExtended = "true";
                        predictRequest.AreaSurchargesRemote = "false";
                        break;

                    case AreaSurchargeChargeCodes.RAS:
                        predictRequest.AreaSurchargesNone = "false";
                        predictRequest.AreaSurchargesDelivery = "false";
                        predictRequest.AreaSurchargesExtended = "false";
                        predictRequest.AreaSurchargesRemote = "true";
                        break;

                    default:
                        predictRequest.AreaSurchargesNone = "true";
                        predictRequest.AreaSurchargesDelivery = "false";
                        predictRequest.AreaSurchargesExtended = "false";
                        predictRequest.AreaSurchargesRemote = "false";
                        break;
                }
            }

            return predictRequest;
        }

        public static RateEngineRequest CreateRateEngineRequestObject(PredictServiceRequestDto predictRequestDto)
        {
            var rateEngineRequest = new RateEngineRequest
            {
                RateGroup = predictRequestDto.RateGroup.Trim(),
                ShipDate = predictRequestDto.ShipDate.ToString().Trim(),
                CommitmentDate = Convert.ToDateTime(predictRequestDto.CommitmentDate.ToString().Trim()),
                Shipper = predictRequestDto.Shipper.Trim(),
                RateType = predictRequestDto.RateType
            };

            rateEngineRequest.Receiver.Address.City = predictRequestDto.Receiver.Address.City.Trim();
            rateEngineRequest.Receiver.Address.State = predictRequestDto.Receiver.Address.State.Trim();
            rateEngineRequest.Receiver.Address.PostalCode = predictRequestDto.Receiver.Address.PostalCode.Trim();
            rateEngineRequest.Receiver.Address.CountryCode = predictRequestDto.Receiver.Address.CountryCode.Trim();
            rateEngineRequest.Receiver.Address.Residential = predictRequestDto.Receiver.Address.Residential;

            rateEngineRequest.Receiver.Contact.Name = predictRequestDto.Receiver.Contact.Name.Trim();
            rateEngineRequest.Receiver.Contact.Email = predictRequestDto.Receiver.Contact.Email.Trim();
            rateEngineRequest.Receiver.Contact.Company = predictRequestDto.Receiver.Contact.Company.Trim();
            rateEngineRequest.Receiver.Contact.Phone = predictRequestDto.Receiver.Contact.Phone.Trim();

            foreach (var predictRequestPackage in predictRequestDto.Packages)
            {
                var package = new Common.MassTransit.Contracts.Package();

                package.Dimensions.UOM = predictRequestPackage.Dimensions.UOM.Trim();
                package.Dimensions.Length = predictRequestPackage.Dimensions.Length;
                package.Dimensions.Width = predictRequestPackage.Dimensions.Width;
                package.Dimensions.Height = predictRequestPackage.Dimensions.Height;

                package.Weight.UOM = predictRequestPackage.Weight.UOM.Trim();
                package.Weight.Value = predictRequestPackage.Weight.Value;

                package.SignatureRequired = predictRequestPackage.SignatureRequired;
                package.AdultSignatureRequired = predictRequestPackage.AdultSignatureRequired;

                rateEngineRequest.Packages.Add(package);
            }

            return rateEngineRequest;
        }

        public static PredictCostRequestDto CreatePredictCostRequestObject(PredictServiceRequestDto predictRequestDto, string carrierServiceName)
        {
            var predictCostRequestDto = new PredictCostRequestDto
            {
                ModelFileName = $"{predictRequestDto.RateGroup.Trim()}_COST",
                CarrierServiceName = carrierServiceName,
                ShipDay = predictRequestDto.ShipDate.DayOfWeek.ToString().Substring(0, 3).ToUpper(),
                PostalCode = predictRequestDto.Receiver.Address.PostalCode,
                Residential = predictRequestDto.Receiver.Address.Residential
            };

            float ratedWeight = 0;

            foreach (var package in predictRequestDto.Packages)
            {
                ratedWeight += (float)package.Weight.Value;
                predictCostRequestDto.SignatureRequired = package.SignatureRequired;
                predictCostRequestDto.AdultSignatureRequired = package.AdultSignatureRequired;
            }

            predictCostRequestDto.RatedWeight = ratedWeight;

            return predictCostRequestDto;
        }

        private async Task LogMessageAsync(Level level, string message)
        {
            var _log = new Log
            {
                Controller = "PredictEngine.Service.Controllers.PredictServiceController",
                Level = level.ToString(),
                Message = message
            };

            await logRepository.CreateAsync(_log);
        }
    }
}
