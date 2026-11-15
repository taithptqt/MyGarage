namespace MyGarage.Application.CustomerPortal;

public interface ICustomerPortalStore :
    ICustomerPortalAuthorizationStore,
    ICustomerPortalVehicleStore,
    ICustomerPortalNotificationStore,
    ICustomerPortalDashboardStore
{
}

public sealed record CustomerPortalConversationAccessInfo(long Id, long CustomerId, long? BranchId);

public sealed record CustomerPortalModelInfo(long Id, string ModelName, string BrandName);

public sealed record CustomerPortalOrderSnapshot(
    long Id,
    string? Code,
    string Status,
    bool HasActiveDiagnosis,
    bool HasWaitingParts);
