using Microsoft.ML.Data;

public class IntentPrediction
{
    [ColumnName("PredictedLabel")]
    public string Intent;
    public float[] Score { get; set; }
}


