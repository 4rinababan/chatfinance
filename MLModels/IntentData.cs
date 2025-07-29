using Microsoft.ML.Data;

public class IntentData
{
    [LoadColumn(0)]
    public string Text;

    [LoadColumn(1)]
    public string Intent;
}
