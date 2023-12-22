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
        public string PostalCode { get; set; }
        [LoadColumn(4)]
        public float TotalCost { get; set; }
        [LoadColumn(5)]
        public float RatedWeight { get; set; }
        [LoadColumn(6)]
        public string RatedWeightUOM { get; set; }
        [LoadColumn(7)]
        public string ShipDay { get; set; }
        [LoadColumn(8)]
        public string CommitDeliveryDay { get; set; }
        [LoadColumn(9)]
        public string CommitDeliveryDate { get; set; }
        [LoadColumn(10)]
        public float CommitTransitDays { get; set; }
        [LoadColumn(11)]
        public string Residential { get; set; }
        [LoadColumn(12)]
        public string SignatureRequired { get; set; }
        [LoadColumn(13)]
        public string AdultSignatureRequired { get; set; }

        public static PredictionUnit FromCsv(string csvLine)
        {
            string[] values = csvLine.Split(',');
            PredictionUnit predictionUnit = new PredictionUnit();
            predictionUnit.Id = values[0];
            predictionUnit.RateGroup = values[1];
            predictionUnit.CarrierServiceName = values[2];
            predictionUnit.PostalCode = values[3];
            predictionUnit.TotalCost = float.Parse(values[4]);
            predictionUnit.RatedWeight = float.Parse(values[5]);
            predictionUnit.RatedWeightUOM = values[6];
            predictionUnit.ShipDay = values[7];
            predictionUnit.CommitDeliveryDay = values[8];
            predictionUnit.CommitDeliveryDate = values[9];
            predictionUnit.CommitTransitDays = float.Parse(values[10]);
            predictionUnit.Residential = values[11];
            predictionUnit.SignatureRequired = values[12];
            predictionUnit.AdultSignatureRequired = values[13];

            return predictionUnit;
        }
    }
}
