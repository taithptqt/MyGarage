using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Application.Payments;

public enum PaymentCommandStatus
{
    Success,
    BadRequest,
    NotFound,
    Forbidden
}

public sealed record PaymentCommandResult(PaymentCommandStatus Status, string? ErrorMessage = null)
{
    public static PaymentCommandResult Ok() => new(PaymentCommandStatus.Success);
    public static PaymentCommandResult BadRequest(string errorMessage) => new(PaymentCommandStatus.BadRequest, errorMessage);
    public static PaymentCommandResult NotFound(string? errorMessage = null) => new(PaymentCommandStatus.NotFound, errorMessage);
    public static PaymentCommandResult Forbidden() => new(PaymentCommandStatus.Forbidden);
}

public sealed record PaymentCommandResult<T>(PaymentCommandStatus Status, T? Data = default, string? ErrorMessage = null)
{
    public static PaymentCommandResult<T> Ok(T data) => new(PaymentCommandStatus.Success, data);
    public static PaymentCommandResult<T> BadRequest(string errorMessage) => new(PaymentCommandStatus.BadRequest, default, errorMessage);
    public static PaymentCommandResult<T> NotFound(string? errorMessage = null) => new(PaymentCommandStatus.NotFound, default, errorMessage);
    public static PaymentCommandResult<T> Forbidden() => new(PaymentCommandStatus.Forbidden);
}

public sealed record PaymentTransitionResult(bool Confirmed, PaymentResponseDto Payment);

public interface IPaymentLifecycleCommandService
{
    Task<PaymentCommandResult<PaymentResponseDto>> CreateAsync(
        PaymentCreateDto itemDto,
        long actorUserId,
        CancellationToken ct = default);

    Task<PaymentCommandResult<PaymentResponseDto>> UpdateAsync(
        long id,
        PaymentUpdateDto itemDto,
        long actorUserId,
        CancellationToken ct = default);

    Task<PaymentCommandResult> DeleteAsync(
        long id,
        CancellationToken ct = default);

    Task<PaymentCommandResult<PaymentTransitionResult>> TransitionStatusAsync(
        long id,
        string? targetStatus,
        decimal? amount,
        string? note,
        long? actorUserId,
        long? scopedBranchId,
        CancellationToken ct = default);
}
