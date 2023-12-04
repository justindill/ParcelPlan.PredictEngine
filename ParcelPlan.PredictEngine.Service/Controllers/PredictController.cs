using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using ParcelPlan.Common;
using ParcelPlan.PredictEngine.Service.Models;
using static ParcelPlan.PredictEngine.Service.Dtos;

namespace ParcelPlan.PredictEngine.Service.Controllers
{
    [ApiController]
    [Route("predict")]
    public class PredictController : ControllerBase
    {
        private readonly IConfiguration configuration;
        private readonly IRepository<Log> logRepository;
        private readonly IOptions<ApiBehaviorOptions> apiBehaviorOptions;

        public PredictController(IConfiguration configuration, IRepository<Log> logRepository, IOptions<ApiBehaviorOptions> apiBehaviorOptions)
        {
            this.configuration = configuration;
            this.logRepository = logRepository;
            this.apiBehaviorOptions = apiBehaviorOptions;
        }

        [HttpPost]
        public async Task<ActionResult<PredictResultDto>> PostAsync(PredictRequestDto predictRequestDto)
        {

            if (string.IsNullOrEmpty(predictRequestDto.RateGroup))
            {
                ModelState.AddModelError(nameof(PredictResultDto), $"Please provide a valid rate group in your request.");

                await LogMessageAsync(Level.ERROR, "The prediction engine request was not valid (Bad Request).  Rate group missing.");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            var predictRequestRateGroup = predictRequestDto.RateGroup;

            var predictRequest = CreatePredictRequestObject(predictRequestDto);

            if (predictRequest == null)
            {
                ModelState.AddModelError(nameof(TrainingUnit), $"Please provide a valid request.");

                await LogMessageAsync(Level.ERROR, "The prediction engine request was not valid (Bad Request).");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            if (string.IsNullOrEmpty(predictRequest.PostalCode.ToString()) || predictRequest.PostalCode.ToString().Length < 5)
            {
                ModelState.AddModelError(nameof(PredictResultDto), $"Please provide a valid postal code.");

                await LogMessageAsync(Level.ERROR, "The prediction engine request was not valid (Bad Request).  Postal code is not valid.");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            predictRequest.PostalCode = float.Parse(predictRequest.PostalCode.ToString().Substring(0, 3));

            var modelFilePath = configuration.GetValue<string>("ModelFiles:Path");

            var context = new MLContext();

            var trainedModel = context.Model.Load($"{modelFilePath}{predictRequestRateGroup}.zip", out DataViewSchema modelSchema);

            var predictEngine = context.Model.CreatePredictionEngine<TrainingUnit, PredictResult>(trainedModel);

            var prediction = predictEngine.Predict(predictRequest);

            var predictionResult = new PredictResultDto();

            predictionResult.PredictedService = prediction.PredictedLabelString;

            var entropy = -prediction.Score.Sum(p => p * Math.Log(p));

            predictionResult.Confidence = $"{(100 - (entropy * 100)).ToString("0.00")}%";

            // TODO: If confidence is below a certain level
            //      - Perform a rateshop (via RabbitMQ message to Rate Engine)
            //      - Model Engine will need to consume the Rate Engine response so that new training data can be added to database

            return Ok(predictionResult);
        }

        public static TrainingUnit CreatePredictRequestObject(PredictRequestDto predictRequestDto)
        {
            var predictRequest = new TrainingUnit
            {
                PostalCode = predictRequestDto.PostalCode,
                RatedWeight = predictRequestDto.RatedWeight,
                Residential = predictRequestDto.Residential.ToString(),
                SignatureRequired = predictRequestDto.SignatureRequired.ToString(),
                AdultSignatureRequired = predictRequestDto.AdultSignatureRequired.ToString()
            };

            return predictRequest;
        }

        private async Task LogMessageAsync(Level level, string message)
        {
            var _log = new Log
            {
                Controller = "PredictEngine.Service.Controllers.PredictController",
                Level = level.ToString(),
                Message = message
            };

            await logRepository.CreateAsync(_log);
        }
    }
}
