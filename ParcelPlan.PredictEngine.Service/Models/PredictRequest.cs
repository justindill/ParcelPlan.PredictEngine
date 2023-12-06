namespace ParcelPlan.PredictEngine.Service.Models
{
    public class PredictRequest
    {
        public string CarrierServiceName { get; set; }
        public float PostalCode { get; set; }
        public float RatedWeight { get; set; }
        public string Residential { get; set; }
        public string SignatureRequired { get; set; }
        public string AdultSignatureRequired { get; set; }
    }
}
