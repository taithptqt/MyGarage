namespace MyGarage.Interfaces;

public interface IPromptBuilder
{
    PromptBuildResult Build(
        string userInput,
        IntentDetectionResult intent,
        string intentStateJson,
        GuardrailValidationResult guardrail);
}

public class PromptBuildResult
{
    public string SystemPrompt { get; set; } = string.Empty;

    public string UserPrompt { get; set; } = string.Empty;
}
