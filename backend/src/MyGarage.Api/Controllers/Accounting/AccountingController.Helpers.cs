using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Accounting;

namespace MyGarage.Controllers;

public partial class AccountingController : ControllerBase
{
    private IActionResult ToActionResult(AccountingCommandResult result)
    {
        if (result.ProblemMessage is not null)
        {
            return this.ProblemMessage(result.StatusCode, result.ProblemMessage);
        }

        return result.StatusCode switch
        {
            200 => Ok(result.Payload),
            204 => NoContent(),
            400 => result.Payload is null ? BadRequest() : BadRequest(result.Payload),
            404 => result.Payload is null ? NotFound() : NotFound(result.Payload),
            409 => result.Payload is null ? Conflict() : Conflict(result.Payload),
            _ => StatusCode(result.StatusCode, result.Payload)
        };
    }

    private bool TryGetTableAccess(string tableKey, out AccountingAccess access, out IActionResult deniedResult) =>
        TryAuthorize(_commandFacade.AuthorizeTableAccess(tableKey, GetActorContext()), out access, out deniedResult);

    private bool TryGetFinanceApprovalAccess(out AccountingAccess access, out IActionResult deniedResult) =>
        TryAuthorize(_commandFacade.AuthorizeFinanceApprovalAccess(GetActorContext()), out access, out deniedResult);

    private bool TryGetAccountingAccess(out AccountingAccess access, out IActionResult deniedResult) =>
        TryAuthorize(_commandFacade.AuthorizeAccountingAccess(GetActorContext()), out access, out deniedResult);

    private bool TryAuthorize(AccountingAccessResult auth, out AccountingAccess access, out IActionResult deniedResult)
    {
        if (auth.IsAllowed && auth.Access is not null)
        {
            access = auth.Access;
            deniedResult = null!;
            return true;
        }

        access = null!;
        deniedResult = auth.DeniedStatusCode == 401
            ? this.UnauthorizedProblem(auth.DeniedMessage ?? "Unauthorized.")
            : Forbid();
        return false;
    }

    private AccountingActorContext GetActorContext() => new(
        User.Identity?.IsAuthenticated ?? false,
        this.GetCurrentUserId(),
        this.GetCurrentBranchId(),
        this.GetCurrentRole(),
        this.GetCurrentRoleCode(),
        this.GetCurrentRoleName());
}
