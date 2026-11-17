using MyGarage.Interfaces;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class AppointmentAccessPolicyTests
{
    [Theory]
    [InlineData(100L, false, false, null, true)]
    [InlineData(101L, false, false, null, false)]
    [InlineData(null, false, false, null, false)]
    [InlineData(200L, true, false, 1L, true)]
    [InlineData(200L, true, false, 2L, false)]
    [InlineData(200L, true, false, null, false)]
    [InlineData(201L, true, true, null, true)]
    public void Access_PreservesCustomerBranchAndAdminRules(long? userId, bool staff, bool admin, long? branchId, bool expected)
    {
        var actor = new AppointmentActorContext(userId, staff, admin, branchId, null, null, null);
        Assert.Equal(expected, AppointmentAccessPolicy.CanAccess(100, 1, actor));
    }
}