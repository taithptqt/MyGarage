namespace MyGarage.Interfaces;

public interface IIntentDetector
{
    IntentDetectionResult Detect(string input, string? currentIntentStateJson);
}

public class IntentDetectionResult
{
    public string Intent { get; set; } = "UNKNOWN";

    public decimal Confidence { get; set; }

    public bool IsLowConfidence { get; set; }

    public string Strategy { get; set; } = "DETERMINISTIC";

    public string? Explanation { get; set; }
}
