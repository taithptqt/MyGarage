namespace MyGarage.Application.Interfaces.Stores.Auth
{
    public sealed record AuthCustomerStampSnapshot(string? SecurityStamp);

    public sealed record AuthStaffStampSnapshot(string? SecurityStamp, string? Status);

    public sealed record AuthBranchGeoSnapshot(long Id, string? Name, decimal? Lat, decimal? Lng);

    public interface IAuthStore :
        IAuthSessionValidationStore,
        IAuthCustomerAccountStore,
        IAuthStaffAccountStore,
        IAuthPasswordResetStore
    {
    }
}
