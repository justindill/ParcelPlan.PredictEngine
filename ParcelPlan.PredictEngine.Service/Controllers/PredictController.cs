using MassTransit;
using MassTransit.Transports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using ParcelPlan.Common;
using ParcelPlan.Common.MassTransit.Contracts;
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
        private readonly IRequestClient<PredictEngineRateRequestCreated> rateEngineRequestClient;
        private readonly IPublishEndpoint publishEndpoint;
        private readonly IOptions<ApiBehaviorOptions> apiBehaviorOptions;

        public PredictController(IConfiguration configuration, IRepository<Log> logRepository, 
            IRequestClient<PredictEngineRateRequestCreated> rateEngineRequestClient, IPublishEndpoint publishEndpoint,
            IOptions<ApiBehaviorOptions> apiBehaviorOptions)
        {
            this.configuration = configuration;
            this.logRepository = logRepository;
            this.rateEngineRequestClient = rateEngineRequestClient;
            this.publishEndpoint = publishEndpoint;
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

            var predictRequest = CreatePredictRequestObject(predictRequestDto, predictRequestDto.RateGroup);

            if (predictRequest == null)
            {
                ModelState.AddModelError(nameof(PredictionUnit), $"Please provide a valid request.");

                await LogMessageAsync(Level.ERROR, "The prediction engine request was not valid (Bad Request).");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            predictRequest.PostalCode = float.Parse(predictRequest.PostalCode.ToString().Substring(0, 3));

            var modelFilePath = configuration.GetValue<string>("ModelFiles:Path");

            var context = new MLContext();

            var trainedModel = context.Model.Load($"{modelFilePath}{predictRequest.RateGroup}.zip", out DataViewSchema modelSchema);

            var predictEngine = context.Model.CreatePredictionEngine<PredictionUnit, PredictResult>(trainedModel);

            var prediction = predictEngine.Predict(predictRequest);

            var predictionResult = new PredictResultDto();

            predictionResult.PredictedService = prediction.PredictedLabelString;

            var entropy = -prediction.Score.Sum(p => p * Math.Log(p));

            double confidence = 100 - (entropy * 100);

            predictionResult.Confidence = $"{(confidence).ToString("0.00")}%";

            if (confidence < Convert.ToDouble(configuration.GetValue<string>("ConfidenceThreshold")))
            {
                var rateEngineRequest = CreateRateEngineRequestObject(predictRequestDto);

                var rateEngineResponse = await rateEngineRequestClient.GetResponse<PredictEngineRateResultCreated>(new PredictEngineRateRequestCreated(rateEngineRequest));

                var rateResult = rateEngineResponse.Message;

                predictionResult.Confidence = "100%";

                predictionResult.CarrierRated = true;

                predictionResult.Detail.EstimatedCost = rateResult.TotalCost;

                predictionResult.Detail.EstimatedTransitDays = rateResult.Commit.TransitDays;
            }
            else
            {
                // TODO:
                //          - Create 'TrainingData' class.
                //              - 'TrainingData' class should include a 'RateGroup' property
                //              - On service start, load the contents of each RSG CSV file into a 'TrainingData' object and add to LIST of a LIST of 'TraingData' objects
                //                  - Before adding each object to the list, ensure that the 'RateGroup' property has a value equal to the CSV file name
                //          - Add 'options' section to PredictRequestDto w/ 'estimateCost' and 'estimateTransitDays' boolean flags
                //          - If either flag is true, iterate thru LIST of a LIST of 'TrainingData' objects until appropriate 'RateGroup' is found (matches PredictRequest 'RateGroup' value)
                //              - If not, set 'EstimatedCost' and 'EstimatedTransitDays' in request to null
                //          - Iterate thru LIST of 'TrainingData' objects (each record in RateGroup CSV) and take first record w/ matching postal prefix, winning service and weight
                //          - Parse out 'TotalCost' value (if 'estimateCost' flag in PredictRequest is true)
                //          - Parse out 'Commit.TransitDays' value (if 'estimateTransitDays' flag in PredictRequest is true)
                //          - Modify PredictResultDto to include 'EstimatedCost' and 'EstimatedTransitDays'
                //          - Map these values to the predictionResult object for return to the client
            }

            return Ok(predictionResult);
        }

        public static PredictionUnit CreatePredictRequestObject(PredictRequestDto predictRequestDto, string rateGroup)
        {
            var predictRequest = new PredictionUnit
            {
                RateGroup = rateGroup,
                PostalCode = float.Parse(predictRequestDto.Receiver.Address.PostalCode),
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

            return predictRequest;
        }

        public static RateEngineRequest CreateRateEngineRequestObject(PredictRequestDto predictRequestDto)
        {
            var rateEngineRequest = new RateEngineRequest
            {
                RateGroup = predictRequestDto.RateGroup,
                ShipDate = predictRequestDto.ShipDate,
                CommitmentDate = predictRequestDto.CommitmentDate,
                Shipper = predictRequestDto.Shipper,
                RateType = predictRequestDto.RateType
            };

            rateEngineRequest.Receiver.Address.City = predictRequestDto.Receiver.Address.City;
            rateEngineRequest.Receiver.Address.State = predictRequestDto.Receiver.Address.State;
            rateEngineRequest.Receiver.Address.PostalCode = predictRequestDto.Receiver.Address.PostalCode;
            rateEngineRequest.Receiver.Address.CountryCode = predictRequestDto.Receiver.Address.CountryCode;
            rateEngineRequest.Receiver.Address.Residential = predictRequestDto.Receiver.Address.Residential;

            rateEngineRequest.Receiver.Contact.Name = predictRequestDto.Receiver.Contact.Name;
            rateEngineRequest.Receiver.Contact.Email = predictRequestDto.Receiver.Contact.Email;
            rateEngineRequest.Receiver.Contact.Company = predictRequestDto.Receiver.Contact.Company;
            rateEngineRequest.Receiver.Contact.Phone = predictRequestDto.Receiver.Contact.Phone;

            foreach (var predictRequestPackage in predictRequestDto.Packages)
            {
                var package = new Common.MassTransit.Contracts.Package();

                package.Dimensions.UOM = predictRequestPackage.Dimensions.UOM;
                package.Dimensions.Length = predictRequestPackage.Dimensions.Length;
                package.Dimensions.Width = predictRequestPackage.Dimensions.Width;
                package.Dimensions.Height = predictRequestPackage.Dimensions.Height;

                package.Weight.UOM = predictRequestPackage.Weight.UOM;
                package.Weight.Value = predictRequestPackage.Weight.Value;

                package.SignatureRequired = predictRequestPackage.SignatureRequired;
                package.AdultSignatureRequired = predictRequestPackage.AdultSignatureRequired;

                rateEngineRequest.Packages.Add(package);
            }

            return rateEngineRequest;
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
