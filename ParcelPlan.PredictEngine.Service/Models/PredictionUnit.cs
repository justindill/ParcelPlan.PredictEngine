using Microsoft.ML.Data;

namespace ParcelPlan.PredictEngine.Service.Models
{
    public class PredictionUnit
    {
        [LoadColumn(0)]
        public string Id { get; set; }
        [LoadColumn(1)]
        public string RateGroup { get; set; }
        [LoadColumn(2)]
        public string CarrierServiceName { get; set; }
        [LoadColumn(3)]
        public float PostalCode { get; set; }
        [LoadColumn(4)]
        public float TotalCost { get; set; }
        [LoadColumn(5)]
        public float RatedWeight { get; set; }
        [LoadColumn(6)]
        public string RatedWeightUOM { get; set; }
        [LoadColumn(7)]
        public string CommitDeliveryDay { get; set; }
        [LoadColumn(8)]
        public string CommitDeliveryDate { get; set; }
        [LoadColumn(9)]
        public float CommitTransitDays { get; set; }
        [LoadColumn(10)]
        public string Residential { get; set; }
        [LoadColumn(11)]
        public string SignatureRequired { get; set; }
        [LoadColumn(12)]
        public string AdultSignatureRequired { get; set; }
    }
}
