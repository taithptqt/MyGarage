using MyGarage.Domain.Rules;
using MyGarage.Support;
using Xunit;

namespace MyGarage.Tests;

public sealed class CustomerPortalStageParityTests
{
    [Fact]
    public void ExtractedRules_PreserveLegacyStageAndTimelineForAllStatusCombinations()
    {
        string?[] statuses = [null, "", " unknown ", "NEW", "WAITING_RECEIVE", "RECEIVED", "CHECKED_IN",
            "WAITING", "DIAGNOSING", "WAITING_PARTS", "QUOTING", "WAITING_APPROVAL", "APPROVED",
            "IN_PROGRESS", "REPAIRING", "READY", "WAITING_PAYMENT", "COMPLETED", "CLOSED", "DELIVERED", "CANCELED"];

        foreach (var status in statuses)
        foreach (var hasDiagnosis in new[] { false, true })
        foreach (var hasWaitingParts in new[] { false, true })
        {
            var oldStage = CustomerPortalStageMapper.ResolveStage(status, hasDiagnosis, hasWaitingParts);
            var newStage = CustomerPortalDomainRules.ResolveStage(status, hasDiagnosis, hasWaitingParts);
            Assert.Equal(oldStage, newStage);
            Assert.Equal(CustomerPortalStageMapper.ToVietnameseLabel(oldStage), CustomerPortalDomainRules.ToVietnameseLabel(newStage));
            foreach (var canceled in new[] { false, true })
            {
                var oldTimeline = CustomerPortalStageMapper.BuildTimeline(oldStage, canceled);
                var newTimeline = CustomerPortalDomainRules.BuildTimeline(newStage, canceled);
                Assert.Equal(oldTimeline.Select(x => (x.Code, x.Label, x.State)), newTimeline.Select(x => (x.Code, x.Label, x.State)));
            }
        }
    }
}
