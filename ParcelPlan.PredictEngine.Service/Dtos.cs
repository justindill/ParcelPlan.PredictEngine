using System.ComponentModel.DataAnnotations;

namespace ParcelPlan.PredictEngine.Service
{
    public class Dtos
    {
        public class PredictServiceRequestDto
        {
            [Required] public string RateGroup { get; set; }
            [Required][DataType(DataType.Date)] public DateTime ShipDate { get; set; }
            [DataType(DataType.Date)] public DateTime CommitmentDate { get; set; }
            [Required] public string Shipper { get; set; }
            [Required] public Receiver Receiver { get; set; } = new();
            [Required, MinLength(1, ErrorMessage = "The field RateType must be a string or array type with a minimum length of '1'.")]
            public List<string> RateType { get; set; } = new();
            [Required, MinLength(1, ErrorMessage = "The field Packages must be an array of packages with a minimum length of '1'.")]
            public List<Package> Packages { get; set; } = new();
            public bool EstimateCost { get; set; } = false;
            public bool EstimateTransitDays { get; set; } = false;
        }

        public class PredictCostRequestDto
        {
            [Required] public string RateGroup { get; set; }
            [Required] public string CarrierServiceName { get; set; }
            [Required][DataType(DataType.Date)] public DateTime ShipDate { get; set; }
            [DataType(DataType.Date)] public DateTime CommitmentDate { get; set; }
            [Required] public string Shipper { get; set; }
            [Required] public Receiver Receiver { get; set; } = new();
            [Required, MinLength(1, ErrorMessage = "The field RateType must be a string or array type with a minimum length of '1'.")]
            public List<string> RateType { get; set; } = new();
            [Required, MinLength(1, ErrorMessage = "The field Packages must be an array of packages with a minimum length of '1'.")]
            public List<Package> Packages { get; set; } = new();
            public bool EstimateCost { get; set; } = false;
            public bool EstimateTransitDays { get; set; } = false;
        }

        public class Receiver
        {
            [Required] public Address Address { get; set; } = new();
            [Required] public Contact Contact { get; set; } = new();
        }

        public class Address
        {
            [Required] public string City { get; set; }
            [Required] public string State { get; set; }
            [Required]
            [StringLength(5, ErrorMessage = "Please provide a valid 5-digit postal code.", MinimumLength = 5)]
            [RegularExpression(@"^[^\p{P}\p{Sm}]*$", ErrorMessage = "Please provide a valid 5-digit postal code.")]
            public string PostalCode { get; set; }
            [Required] public string CountryCode { get; set; }
            public bool Residential { get; set; }
        }

        public class Contact
        {
            [Required] public string Name { get; set; }
            [Required] public string Email { get; set; }
            [Required] public string Company { get; set; }
            [Required] public string Phone { get; set; }
        }

        public class Package
        {
            [Required] public Dimensions Dimensions { get; set; } = new();
            [Required] public Weight Weight { get; set; } = new();
            public bool SignatureRequired { get; set; } = false;
            public bool AdultSignatureRequired { get; set; } = false;
        }

        public class Dimensions
        {
            [Required] public string UOM { get; set; }
            [Required] public int Length { get; set; }
            [Required] public int Width { get; set; }
            [Required] public int Height { get; set; }
        }

        public class Weight
        {
            [Required] public string UOM { get; set; }
            [Required] public double Value { get; set; }
        }

        public class PredictServiceResultDto
        {
            public PredictResultStatus Status { get; set; } = new();
            public string PredictedService { get; set; }
            public string Confidence { get; set; }
            public bool CarrierRated { get; set; }
            public Detail Detail { get; set; } = new();
        }

        public class PredictCostResultDto
        {
            public PredictResultStatus Status { get; set; } = new();
            public float PredictedCost { get; set; }
            // public string Confidence { get; set; }
            // public bool CarrierRated { get; set; }
        }

        public class PredictResultStatus
        {
            public int Code { get; set; }
            public string Description { get; set; }
        }

        public class Detail
        {
            public decimal EstimatedCost { get; set; }
            // public int EstimatedTransitDays { get; set; }
        }

        public class Commit
        {
            public string DeliveryDay { get; set; }
            public DateTime DeliveryDate { get; set; }
            public int TransitDays { get; set; }
        }
    }
}
