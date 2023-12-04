using System.ComponentModel.DataAnnotations;

namespace ParcelPlan.PredictEngine.Service
{
    public class Dtos
    {
        public record PredictRequestDto(
            [Required] string RateGroup,
            [Required] float PostalCode,
            [Required] float RatedWeight,
            bool Residential,
            bool SignatureRequired,
            bool AdultSignatureRequired
        );

        public class PredictResultDto
        {
            public string PredictedService { get; set; }
            public string Confidence { get; set; }
        }
    }
}
