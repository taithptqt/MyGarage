using MyGarage.Domain.Payments;
using Xunit;

namespace MyGarage.Tests;

public sealed class DocumentPaymentStatusRulesTests
{
    [Theory]
    [InlineData(0, 0, "PAID")]
    [InlineData(0, 100, "UNPAID")]
    [InlineData(1, 100, "PARTIAL")]
    [InlineData(100, 100, "PAID")]
    [InlineData(101, 100, "PAID")]
    [InlineData(-1, 100, "UNPAID")]
    public void Resolve_PreservesExistingStatus(decimal paid, decimal total, string expected)
        => Assert.Equal(expected, DocumentPaymentStatusRules.Resolve(paid, total));
}
