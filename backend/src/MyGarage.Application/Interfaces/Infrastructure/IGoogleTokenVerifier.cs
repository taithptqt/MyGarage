using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public sealed record GoogleTokenPayload(
        string? Email,
        bool EmailVerified,
        string? Subject,
        string? Name);

    public interface IGoogleTokenVerifier
    {
        Task<GoogleTokenPayload> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
    }
}
