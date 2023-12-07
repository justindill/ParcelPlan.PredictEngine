using MassTransit;
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
        private readonly IBus iBus;
        private readonly IOptions<ApiBehaviorOptions> apiBehaviorOptions;

        public PredictController(IConfiguration configuration, IRepository<Log> logRepository, 
            IRequestClient<PredictEngineRateRequestCreated> rateEngineRequestClient, 
            IBus iBus, IOptions<ApiBehaviorOptions> apiBehaviorOptions)
        {
            this.configuration = configuration;
            this.logRepository = logRepository;
            this.rateEngineRequestClient = rateEngineRequestClient;
            this.iBus = iBus;
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


            var context = new MLContext();

            var modelFilePath = this.configuration.GetValue<string>("ModelFiles:Path");

            if (!Directory.Exists(modelFilePath))
            {
                ModelState.AddModelError(nameof(PredictionUnit), $"Model file path could not be found: {modelFilePath}");

                await LogMessageAsync(Level.ERROR, $"Model file path could not be found: {modelFilePath}");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            if (!System.IO.File.Exists($"{modelFilePath}{predictRequest.RateGroup}.zip"))
            {
                ModelState.AddModelError(nameof(PredictionUnit), $"Model file could not be found: {modelFilePath}{predictRequest.RateGroup}.zip");

                await LogMessageAsync(Level.ERROR, $"Model file could not be found: {modelFilePath}{predictRequest.RateGroup}.zip");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            var trainedModel = context.Model.Load($"{modelFilePath}{predictRequest.RateGroup}.zip", out DataViewSchema modelSchema);

            if (trainedModel == null)
            {
                ModelState.AddModelError(nameof(PredictionUnit), $"Model file could not be found: {modelFilePath}{predictRequest.RateGroup}.zip");

                await LogMessageAsync(Level.ERROR, $"Model file could not be found: {modelFilePath}{predictRequest.RateGroup}.zip");

                var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                return BadRequest(predictResult);
            }

            var predictEngine = context.Model.CreatePredictionEngine<PredictionUnit, PredictResult>(trainedModel);


            var prediction = predictEngine.Predict(predictRequest);

            var predictionResult = new PredictResultDto();

            double confidence = 0;

            if (prediction != null)
            {
                predictionResult.PredictedService = prediction.PredictedLabelString;

                var entropy = -prediction.Score.Sum(p => p * Math.Log(p));

                confidence = 100 - (entropy * 100);

                predictionResult.Confidence = $"{(confidence).ToString("0.00")}%";
            }

            if (confidence < Convert.ToDouble(configuration.GetValue<string>("ConfidenceThreshold")))
            {
                var rateEngineRequest = CreateRateEngineRequestObject(predictRequestDto);

                var rateEngineResponse = await rateEngineRequestClient.GetResponse<PredictEngineRateResultCreated>(new PredictEngineRateRequestCreated(rateEngineRequest));

                var rateResult = rateEngineResponse.Message;

                predictionResult.Confidence = "100%";

                predictionResult.CarrierRated = true;

                predictionResult.Detail.EstimatedCost = rateResult.TotalCost;

                predictionResult.Detail.EstimatedTransitDays = rateResult.Commit.TransitDays;

                await iBus.Publish(rateResult.AsDto());
            }
            else
            {
                var estimateCost = predictRequestDto.EstimateCost == true ? true : false;

                var estimateTransitDays = predictRequestDto.EstimateTransitDays == true ? true : false;

                var datasetFilePath = this.configuration.GetValue<string>("DatasetFiles:Path");


                if (estimateCost || estimateTransitDays)
                {
                    if (!string.IsNullOrEmpty(datasetFilePath))
                    { 
                        if (!Directory.Exists(datasetFilePath))
                        {
                            ModelState.AddModelError(nameof(PredictionUnit), $"Dataset file path could not be found: {datasetFilePath}");

                            await LogMessageAsync(Level.ERROR, $"Dataset file path could not be found: {datasetFilePath}");

                            var predictResult = apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

                            return BadRequest(predictResult);
                        }

                        var files = from file in Directory.EnumerateFiles(datasetFilePath) select file;

                        var trainingData = new List<PredictionUnit>();

                        foreach (var file in files)
                        {
                            var filename = Path.GetFileName(file).Split('.')[0];

                            if (filename == predictRequest.RateGroup)
                            {
                                trainingData = System.IO.File.ReadAllLines(file)
                                    .Skip(1)
                                    .Select(v => PredictionUnit.FromCsv(v))
                                    .ToList();

                                break;
                            }
                        }

                        if (trainingData != null)
                        {
                            var closestMatch = new PredictionUnit();

                            if (prediction != null)
                            {
                                closestMatch = trainingData.Where(td => td.CarrierServiceName == prediction.PredictedLabelString
                                && td.PostalCode.ToString() == predictRequest.PostalCode.ToString().Substring(0, 3)
                                && td.RatedWeight == Math.Ceiling(predictRequest.RatedWeight)).FirstOrDefault();
                            }

                            if (closestMatch != null)
                            {
                                predictionResult.Detail.EstimatedCost = estimateCost
                                    ? Convert.ToDecimal(closestMatch.TotalCost)
                                    : 0;

                                predictionResult.Detail.EstimatedTransitDays = estimateTransitDays
                                    ? Convert.ToInt32(closestMatch.CommitTransitDays)
                                    : 0;
                            }
                        }
                        else
                        {
                            await LogMessageAsync(Level.WARNING, 
                                $"No training data found when attempting to estimate cost and transit days.  Rate Group: {predictRequest.RateGroup}");
                        }
                    }
                }
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
