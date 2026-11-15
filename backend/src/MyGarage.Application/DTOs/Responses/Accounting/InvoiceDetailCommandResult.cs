namespace MyGarage.DTOs.Responses
{
    public enum InvoiceDetailCommandStatus
    {
        Success = 0,
        NotFound = 1,
        BadRequest = 2
    }

    public sealed record InvoiceDetailCommandResult<T>(
        InvoiceDetailCommandStatus Status,
        T? Data = default,
        string? ErrorMessage = null)
    {
        public static InvoiceDetailCommandResult<T> Ok(T data)
            => new(InvoiceDetailCommandStatus.Success, data, null);

        public static InvoiceDetailCommandResult<T> NotFound()
            => new(InvoiceDetailCommandStatus.NotFound, default, null);

        public static InvoiceDetailCommandResult<T> BadRequest(string errorMessage)
            => new(InvoiceDetailCommandStatus.BadRequest, default, errorMessage);
    }
}
