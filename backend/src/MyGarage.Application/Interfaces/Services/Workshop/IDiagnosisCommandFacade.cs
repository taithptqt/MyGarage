namespace MyGarage.Interfaces
{
    public sealed record DiagnosisCommandResult(int StatusCode, object? Payload = null)
    {
        public static DiagnosisCommandResult Ok(object? payload = null) => new(200, payload);
        public static DiagnosisCommandResult BadRequest(string message) => new(400, new { message });
        public static DiagnosisCommandResult NotFound(string? message = null) => new(404, message == null ? null : new { message });
        public static DiagnosisCommandResult Forbid() => new(403, null);
        public static DiagnosisCommandResult Conflict(object payload) => new(409, payload);
    }

    public sealed record DiagnosisCommandResult<T>(int StatusCode, T? Data = default, object? ErrorPayload = null)
    {
        public static DiagnosisCommandResult<T> Ok(T data) => new(200, data, null);
        public static DiagnosisCommandResult<T> BadRequest(string message) => new(400, default, new { message });
        public static DiagnosisCommandResult<T> NotFound(string? message = null) => new(404, default, message == null ? null : new { message });
        public static DiagnosisCommandResult<T> Forbid() => new(403, default, null);
        public static DiagnosisCommandResult<T> Conflict(object payload) => new(409, default, payload);
    }

    public interface IDiagnosisCommandFacade :
        IDiagnosisCrudCommandFacade,
        IDiagnosisQueueCommandFacade,
        IDiagnosisApprovalCommandFacade
    {
    }
}
