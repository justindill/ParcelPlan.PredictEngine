using Microsoft.ML.Data;

namespace ParcelPlan.PredictEngine.Service.Models
{
    public class ServicePredictionUnit
    {
        [LoadColumn(0)]
        public string Id { get; set; }
        [LoadColumn(1)]
        public string RateGroup { get; set; }
        [LoadColumn(2)]
        public string CarrierServiceName { get; set; }
        [LoadColumn(3)]
        public string PostalCodePrefix { get; set; }
        [LoadColumn(4)]
        public string PostalCode { get; set; }
        [LoadColumn(5)]
        public float TotalCost { get; set; }
        [LoadColumn(6)]
        public float RatedWeight { get; set; }
        [LoadColumn(7)]
        public string RatedWeightUOM { get; set; }
        [LoadColumn(8)]
        public string ShipDate { get; set; }
        [LoadColumn(9)]
        public string ShipDay { get; set; }
        [LoadColumn(10)]
        public string CommitmentDate { get; set; }
        [LoadColumn(11)]
        public string CommitDeliveryDay { get; set; }
        [LoadColumn(12)]
        public string CommitDeliveryDate { get; set; }
        [LoadColumn(13)]
        public float CommitTransitDays { get; set; }
        [LoadColumn(14)]
        public string AreaSurchargesNone { get; set; }
        [LoadColumn(15)]
        public string AreaSurchargesDelivery { get; set; }
        [LoadColumn(16)]
        public string AreaSurchargesExtended { get; set; }
        [LoadColumn(17)]
        public string AreaSurchargesRemote { get; set; }
        [LoadColumn(18)]
        public string Residential { get; set; }
        [LoadColumn(19)]
        public string SignatureRequired { get; set; }
        [LoadColumn(20)]
        public string AdultSignatureRequired { get; set; }

        public static ServicePredictionUnit FromCsv(string csvLine)
        {
            string[] values = csvLine.Split(',');

            ServicePredictionUnit predictionUnit = new ServicePredictionUnit();

            predictionUnit.Id = values[0];
            predictionUnit.RateGroup = values[1];
            predictionUnit.CarrierServiceName = values[2];
            predictionUnit.PostalCodePrefix = values[3];
            predictionUnit.PostalCode = values[4];
            predictionUnit.TotalCost = float.Parse(values[5]);
            predictionUnit.RatedWeight = float.Parse(values[6]);
            predictionUnit.RatedWeightUOM = values[7];
            predictionUnit.ShipDate = values[8];
            predictionUnit.ShipDay = values[9];
            predictionUnit.CommitmentDate = values[10];
            predictionUnit.CommitDeliveryDay = values[11];
            predictionUnit.CommitDeliveryDate = values[12];
            predictionUnit.CommitTransitDays = float.Parse(values[13]);
            predictionUnit.AreaSurchargesNone = values[14];
            predictionUnit.AreaSurchargesNone = values[15];
            predictionUnit.AreaSurchargesNone = values[16];
            predictionUnit.AreaSurchargesNone = values[17];
            predictionUnit.Residential = values[18];
            predictionUnit.SignatureRequired = values[19];
            predictionUnit.AdultSignatureRequired = values[20];

            return predictionUnit;
        }
    }
}
