namespace MyGarage.Interfaces;

public interface IGuardrailValidator
{
    GuardrailValidationResult Validate(string input);
}

public class GuardrailValidationResult
{
    public bool IsAllowed { get; set; }

    public bool IsRejected { get; set; }

    public bool ShouldHandoff { get; set; }

    public string? ViolationCode { get; set; }

    public string? RejectionMessage { get; set; }

    public string SanitizedInput { get; set; } = string.Empty;
}
