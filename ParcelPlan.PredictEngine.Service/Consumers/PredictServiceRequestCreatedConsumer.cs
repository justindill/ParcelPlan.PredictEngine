using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ParcelPlan.Common;
using ParcelPlan.Common.MassTransit.Contracts;
using ParcelPlan.PredictEngine.Service.Controllers;
using static ParcelPlan.PredictEngine.Service.Dtos;

namespace ParcelPlan.PredictEngine.Service.Consumers
{
    public class PredictServiceRequestCreatedConsumer : IConsumer<PredictEngineRequestCreated>
    {
        private readonly PredictServiceController predictServiceController;
        private readonly IRepository<Log> logRepository;

        public PredictServiceRequestCreatedConsumer(PredictServiceController predictServiceController, IRepository<Log> logRepository)
        {
            this.predictServiceController = predictServiceController;
            this.logRepository = logRepository;
        }

        public async Task Consume(ConsumeContext<PredictEngineRequestCreated> context)
        {
            var predictRequest = context.Message.PredictEngineRequest;

            var predictRequestDto = new PredictServiceRequestDto()
            {
                RateGroup = predictRequest.RateGroup,
                ShipDate = predictRequest.ShipDate,
                CommitmentDate = predictRequest.CommitmentDate,
                Shipper = predictRequest.Shipper,
                RateType = predictRequest.RateType,
                EstimateCost = predictRequest.EstimateCost,
                EstimateTransitDays = predictRequest.EstimateTransitDays
            };

            predictRequestDto.Receiver.Address.City = predictRequest.Receiver.Address.City;
            predictRequestDto.Receiver.Address.State = predictRequest.Receiver.Address.State;
            predictRequestDto.Receiver.Address.PostalCode = predictRequest.Receiver.Address.PostalCode;
            predictRequestDto.Receiver.Address.CountryCode = predictRequest.Receiver.Address.CountryCode;
            predictRequestDto.Receiver.Address.Residential = predictRequest.Receiver.Address.Residential;

            predictRequestDto.Receiver.Contact.Name = predictRequest.Receiver.Contact.Name;
            predictRequestDto.Receiver.Contact.Email = predictRequest.Receiver.Contact.Email;
            predictRequestDto.Receiver.Contact.Company = predictRequest.Receiver.Contact.Company;
            predictRequestDto.Receiver.Contact.Phone = predictRequest.Receiver.Contact.Phone;

            foreach (var package in predictRequest.Packages)
            {
                var predictRequestPackage = new Dtos.Package();

                predictRequestPackage.Dimensions.UOM = package.Dimensions.UOM;
                predictRequestPackage.Dimensions.Length = package.Dimensions.Length;
                predictRequestPackage.Dimensions.Width = package.Dimensions.Width;
                predictRequestPackage.Dimensions.Height = package.Dimensions.Height;

                predictRequestPackage.Weight.UOM = package.Weight.UOM;
                predictRequestPackage.Weight.Value = package.Weight.Value;

                predictRequestPackage.SignatureRequired = package.SignatureRequired;
                predictRequestPackage.AdultSignatureRequired = package.AdultSignatureRequired;

                predictRequestDto.Packages.Add(predictRequestPackage);
            }

            var predictResultOkObject = predictServiceController.PostAsync(predictRequestDto).Result.Result as OkObjectResult;

            if (predictResultOkObject != null && predictResultOkObject.Value != null)
            {
                var predictResult = JsonConvert.SerializeObject(predictResultOkObject.Value as PredictServiceResultDto, Formatting.Indented);

                var predictResultCreated = JsonConvert.DeserializeObject<PredictResultCreated>(predictResult);

                if (predictResultCreated != null)
                {
                    await context.RespondAsync(predictResultCreated);
                }
            }
        }

        private async Task LogMessageAsync(Level level, string message)
        {
            var _log = new Log
            {
                Controller = "ParcelPlan.PredictEngine.Service.Consumers.PredictEngineRequestCreatedConsumer",
                Level = level.ToString(),
                Message = message
            };

            await logRepository.CreateAsync(_log);
        }
    }
}
