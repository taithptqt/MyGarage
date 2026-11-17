using MyGarage.Interfaces;

namespace MyGarage.Services;

internal static class AppointmentAccessPolicy
{
    internal static bool CanAccess(long customerId, long branchId, AppointmentActorContext actor)
    {
        if (actor.IsStaffUser)
        {
            return actor.IsAdminStaff || branchId == actor.CurrentBranchId;
        }

        return actor.CurrentUserId.HasValue && customerId == actor.CurrentUserId.Value;
    }
}