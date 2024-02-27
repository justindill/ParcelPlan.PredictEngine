using Microsoft.ML.Data;

namespace ParcelPlan.PredictEngine.Service.Models
{
    public class PredictCostResult
    {
        [ColumnName("Score")]
        public float Score { get; set; }
    }
}
