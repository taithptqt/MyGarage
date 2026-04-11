using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IAuthSecurityStampValidator
    {
        Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    }
}
