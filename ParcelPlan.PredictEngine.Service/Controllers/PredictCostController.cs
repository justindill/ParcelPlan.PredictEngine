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
            var predictRequest = await CreatePredictCostRequestObjectAsync(predictCostRequestDto);

            if (predictRequest == null)
            {
                ModelState.AddModelError(nameof(CostPredictionUnit), $"Please provide a valid request.");

                await LogMessageAsync(Level.ERROR, "The prediction engine request was not valid (Bad Request).");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            var context = new MLContext();

            var costModelFilePath = this.configuration.GetValue<string>("ModelFiles:CostModelPath");

            if (!Directory.Exists(costModelFilePath))
            {
                ModelState.AddModelError(nameof(CostPredictionUnit), $"Model file path could not be found: {costModelFilePath}");

                await LogMessageAsync(Level.ERROR, $"Model file path could not be found: {costModelFilePath}");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            if (!System.IO.File.Exists($"{costModelFilePath}{predictCostRequestDto.ModelFileName}.zip"))
            {
                ModelState.AddModelError(nameof(CostPredictionUnit), $"Model file could not be found: {costModelFilePath}{predictCostRequestDto.ModelFileName}.zip");

                await LogMessageAsync(Level.ERROR, $"Model file could not be found: {costModelFilePath}{predictCostRequestDto.ModelFileName}.zip");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            var trainedModel = context.Model.Load($"{costModelFilePath}{predictCostRequestDto.ModelFileName}.zip", out DataViewSchema modelSchema);

            if (trainedModel == null)
            {
                ModelState.AddModelError(nameof(CostPredictionUnit), $"Model file could not be found: {costModelFilePath}{predictCostRequestDto.ModelFileName}.zip");

                await LogMessageAsync(Level.ERROR, $"Model file could not be found: {costModelFilePath}{predictCostRequestDto.ModelFileName}.zip");

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

        public async Task<CostPredictionUnit> CreatePredictCostRequestObjectAsync(PredictCostRequestDto predictCostRequestDto)
        {
            var predictRequest = new CostPredictionUnit
            {
                CarrierServiceName = predictCostRequestDto.CarrierServiceName,
                ShipDay = predictCostRequestDto.ShipDay,
                PostalCodePrefix = predictCostRequestDto.PostalCode.ToString().Trim().Substring(0, 3).ToUpper(),
                PostalCode = predictCostRequestDto.PostalCode,
                RatedWeight = predictCostRequestDto.RatedWeight,
                Residential = predictCostRequestDto.Residential.ToString(),
                SignatureRequired = predictCostRequestDto.SignatureRequired.ToString(),
                AdultSignatureRequired = predictCostRequestDto.AdultSignatureRequired.ToString(),
            };

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
