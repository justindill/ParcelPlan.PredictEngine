using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using ParcelPlan.Common;
using ParcelPlan.PredictEngine.Service.Entities;
using ParcelPlan.PredictEngine.Service.Models;
using static ParcelPlan.PredictEngine.Service.Dtos;

namespace ParcelPlan.PredictEngine.Service.Controllers
{
    [ApiController]
    [Route("predict/cost")]
    public class PredictCostController : ControllerBase
    {
        private readonly IConfiguration configuration;
        private readonly IRepository<Log> logRepository;
        private readonly IRepository<LocaleDataEntity> localeRepository;
        private readonly IRepository<AS_LocaleDataEntity> as_localeDataRepository;
        private readonly IOptions<ApiBehaviorOptions> apiBehaviorOptions;

        public PredictCostController(IConfiguration configuration, IRepository<Log> logRepository, IOptions<ApiBehaviorOptions> apiBehaviorOptions,
            IRepository<LocaleDataEntity> localeRepository, IRepository<AS_LocaleDataEntity> as_localeDataRepository)
        {
            this.configuration = configuration;
            this.logRepository = logRepository;
            this.localeRepository = localeRepository;
            this.as_localeDataRepository = as_localeDataRepository;
            this.apiBehaviorOptions = apiBehaviorOptions;
        }

        [HttpPost]
        public async Task<ActionResult<PredictCostResultDto>> PostAsync(PredictCostRequestDto predictCostRequestDto)
        {
            if (string.IsNullOrEmpty(predictCostRequestDto.RateGroup))
            {
                ModelState.AddModelError(nameof(PredictCostResultDto), $"Please provide a valid rate group in your request.");

                await LogMessageAsync(Level.ERROR, "The prediction engine request was not valid (Bad Request).  Rate group missing.");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            var predictRequest = await CreatePredictCostRequestObjectAsync(predictCostRequestDto, predictCostRequestDto.RateGroup);

            if (predictRequest == null)
            {
                ModelState.AddModelError(nameof(CostPredictionUnit), $"Please provide a valid request.");

                await LogMessageAsync(Level.ERROR, "The prediction engine request was not valid (Bad Request).");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            predictRequest.PostalCodePrefix = predictRequest.PostalCode.ToString().Substring(0, 3);

            var context = new MLContext();

            var modelFilePath = this.configuration.GetValue<string>("ModelFiles:Path");

            if (!Directory.Exists(modelFilePath))
            {
                ModelState.AddModelError(nameof(CostPredictionUnit), $"Model file path could not be found: {modelFilePath}");

                await LogMessageAsync(Level.ERROR, $"Model file path could not be found: {modelFilePath}");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            if (!System.IO.File.Exists($"{modelFilePath}{predictRequest.RateGroup}.zip"))
            {
                ModelState.AddModelError(nameof(CostPredictionUnit), $"Model file could not be found: {modelFilePath}{predictRequest.RateGroup}.zip");

                await LogMessageAsync(Level.ERROR, $"Model file could not be found: {modelFilePath}{predictRequest.RateGroup}.zip");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            var trainedModel = context.Model.Load($"{modelFilePath}{predictRequest.RateGroup}.zip", out DataViewSchema modelSchema);

            if (trainedModel == null)
            {
                ModelState.AddModelError(nameof(CostPredictionUnit), $"Model file could not be found: {modelFilePath}{predictRequest.RateGroup}.zip");

                await LogMessageAsync(Level.ERROR, $"Model file could not be found: {modelFilePath}{predictRequest.RateGroup}.zip");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            PredictCostResult prediction = new();

            try
            {
                var predictEngine = context.Model.CreatePredictionEngine<CostPredictionUnit, PredictCostResult>(trainedModel);

                prediction = predictEngine.Predict(predictRequest);
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(nameof(CostPredictionUnit), $"Model Error:  Invalid cost prediction model.  " +
                    $"Please ensure that you are using a valid cost prediction model.");

                await LogMessageAsync(Level.ERROR, $"Model Error:  Invalid cost prediction model.  " +
                    $"Please ensure that you are using a valid cost prediction model.");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }
            

            var predictionResult = new PredictCostResultDto();

            if (prediction != null)
            {
                predictionResult.PredictedCost = prediction.Score < 0 ? 0 : prediction.Score;

                predictionResult.Status.Code = 1;
                predictionResult.Status.Description = "Success";
            }
            else
            {
                predictionResult.Status.Code = 0;
                predictionResult.Status.Description = "Unable to estimate total cost.";
            }

            return Ok(predictionResult);
        }

        public async Task<CostPredictionUnit> CreatePredictCostRequestObjectAsync(PredictCostRequestDto predictCostRequestDto, string rateGroup)
        {
            var predictRequest = new CostPredictionUnit
            {
                RateGroup = rateGroup,
                CarrierServiceName = predictCostRequestDto.CarrierServiceName,
                PostalCodePrefix = predictCostRequestDto.Receiver.Address.PostalCode.Substring(0, 3),
                PostalCode = predictCostRequestDto.Receiver.Address.PostalCode,
                Residential = predictCostRequestDto.Receiver.Address.Residential.ToString()
            };

            float ratedWeight = 0;

            foreach (var package in predictCostRequestDto.Packages)
            {
                ratedWeight += (float)package.Weight.Value;
                predictRequest.SignatureRequired = package.SignatureRequired.ToString();
                predictRequest.AdultSignatureRequired = package.AdultSignatureRequired.ToString();
            }

            predictRequest.RatedWeight = ratedWeight;

            predictRequest.ShipDay = predictCostRequestDto.ShipDate.ToString("ddd").ToUpper();


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

        private async Task LogMessageAsync(Level level, string message)
        {
            var _log = new Log
            {
                Controller = "PredictEngine.Service.Controllers.PredictCostController",
                Level = level.ToString(),
                Message = message
            };

            await logRepository.CreateAsync(_log);
        }
    }
}
