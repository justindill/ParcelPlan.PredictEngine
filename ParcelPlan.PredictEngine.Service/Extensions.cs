using ParcelPlan.Common.MassTransit.Contracts;

namespace ParcelPlan.PredictEngine.Service
{
    public static class Extensions
    {
        public static PredictEngineRetrainUnitRequestCreated AsDto(this PredictEngineRateResultCreated predictEngineRateResultCreated)
        {
            var retrainUnitRequest = new PredictEngineRetrainUnitRequestCreated
            {
                RateGroup = predictEngineRateResultCreated.RateGroup,
                CarrierServiceName = predictEngineRateResultCreated.CarrierServiceName,
                PostalCode = predictEngineRateResultCreated.PostalCode,
                TotalCost = predictEngineRateResultCreated.TotalCost,
                RatedWeight = predictEngineRateResultCreated.RatedWeight,
                RatedWeightUOM = predictEngineRateResultCreated.RatedWeightUOM,
                ShipDate = predictEngineRateResultCreated.ShipDate,
                ShipDay = predictEngineRateResultCreated.ShipDay,
                CommitmentDate = predictEngineRateResultCreated.CommitmentDate,
                Results = predictEngineRateResultCreated.Results,
                Residential = predictEngineRateResultCreated.Residential,
                SignatureRequired = predictEngineRateResultCreated.SignatureRequired,
                AdultSignatureRequired = predictEngineRateResultCreated.AdultSignatureRequired
            };

            retrainUnitRequest.Commit.DeliveryDay = predictEngineRateResultCreated.Commit.DeliveryDay;
            retrainUnitRequest.Commit.DeliveryDate = predictEngineRateResultCreated.Commit.DeliveryDate;
            retrainUnitRequest.Commit.TransitDays = predictEngineRateResultCreated.Commit.TransitDays;

            return retrainUnitRequest;
        }
    }
}
