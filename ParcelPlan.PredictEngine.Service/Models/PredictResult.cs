using Microsoft.ML.Data;

namespace ParcelPlan.PredictEngine.Service.Models
{
    public class PredictResult
    {
        public string PredictedService { get; set; }
        public string Confidence { get; set; }

        [ColumnName("PredictedLabel")]
        public uint PredictedLabel { get; set; }

        [ColumnName("PredictedLabelString")]
        public string PredictedLabelString { get; set; }

        [ColumnName("Score")]
        public float[] Score { get; set; }
    }
}
