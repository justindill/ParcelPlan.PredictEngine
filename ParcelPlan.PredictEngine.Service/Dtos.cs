using System.ComponentModel.DataAnnotations;

namespace ParcelPlan.PredictEngine.Service
{
    public class Dtos
    {
        public class PredictRequestDto
        {
            [Required] public string RateGroup { get; set; }
            [Required] public DateTime ShipDate { get; set; }
            public DateTime CommitmentDate { get; set; }
            [Required] public string Shipper { get; set; }
            [Required] public Receiver Receiver { get; set; } = new();
            [Required] public List<string> RateType { get; set; } = new();
            [Required] public List<Package> Packages { get; set; } = new();
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

        public class PredictResultDto
        {
            public string PredictedService { get; set; }
            public string Confidence { get; set; }
            public bool CarrierRated { get; set; }
            public Detail Detail { get; set; } = new();
        }

        public class Detail
        {
            public decimal EstimatedCost { get; set; }
            public int EstimatedTransitDays { get; set; }
        }

        public class Commit
        {
            public string DeliveryDay { get; set; }
            public DateTime DeliveryDate { get; set; }
            public int TransitDays { get; set; }
        }
    }
}
