namespace ParcelPlan.PredictEngine.Service.Entities
{
    public class RateResult
    {
        public Guid Id { get; set; }
        public string RateGroup { get; set; }
        public string CarrierServiceName { get; set; }
        public string PostalCode { get; set; }
        public decimal TotalCost { get; set; }
        public decimal RatedWeight { get; set; }
        public string RatedWeightUOM { get; set; }
        public Commit Commit { get; set; } = new();
        public bool Residential { get; set; }
        public bool SignatureRequired { get; set; }
        public bool AdultSignatureRequired { get; set; }
    }

    public class Commit
    {
        public string DeliveryDay { get; set; }
        public DateTime DeliveryDate { get; set; }
        public int TransitDays { get; set; }
    }
}
