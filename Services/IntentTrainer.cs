using Microsoft.ML;

public class IntentTrainer
{
    private static readonly string ModelPath = "MLModels/IntentModel.zip";
    private readonly MLContext mlContext;
    private PredictionEngine<IntentData, IntentPrediction> predEngine;

    public IntentTrainer()
    {
        mlContext = new MLContext();

        if (!File.Exists(ModelPath))
            TrainAndSaveModel();

        LoadModel();
    }

    private void TrainAndSaveModel()
    {
        var data = mlContext.Data.LoadFromTextFile<IntentData>("Data/IntentData.csv", hasHeader: true, separatorChar: ';');
        var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(IntentData.Text))
            .Append(mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(IntentData.Intent)))
            .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
            .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        var model = pipeline.Fit(data);
        mlContext.Model.Save(model, data.Schema, ModelPath);
    }

    private void LoadModel()
    {
        var model = mlContext.Model.Load(ModelPath, out _);
        predEngine = mlContext.Model.CreatePredictionEngine<IntentData, IntentPrediction>(model);
    }

    public (string intent, float score) PredictIntent(string input)
    {
        var result = predEngine.Predict(new IntentData { Text = input });
        return (result.Intent, result.Score.Max());
    }

    public (string intent, float confidence) PredictIntentWithConfidence(string input)
    {
        var prediction = predEngine.Predict(new IntentData { Text = input });

        // ambil nilai skor tertinggi (confidence dari label yang dipilih)
        var maxScore = prediction.Score.Max();

        return (prediction.Intent, maxScore);
    }

    public (string intent, float score) PredictWithScore(string input)
    {
        var result = predEngine.Predict(new IntentData { Text = input });

        // Ambil index intent yang diprediksi
        int maxIndex = Array.IndexOf(result.Score, result.Score.Max());

        // Ambil nilai confidence tertinggi sebagai score
        float confidence = result.Score[maxIndex];

        return (result.Intent, confidence);
    }

}
