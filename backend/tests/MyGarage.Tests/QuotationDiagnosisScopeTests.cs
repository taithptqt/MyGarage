using System.Reflection;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class QuotationDiagnosisScopeTests
{
    [Fact]
    public void CurrentScope_ExcludesOldRecommendationsButKeepsAllCurrentEventAssignments()
    {
        var at = new DateTime(2026, 1, 1);
        var logs = new List<DiagnosisAssignment>
        {
            new() { Id = 1, DiagnosisId = 10, CreatedAt = at, EventType = "STARTED" },
            new() { Id = 2, DiagnosisId = 10, CreatedAt = at.AddMinutes(1), EventType = "REDIAGNOSE_REQUESTED" },
            new() { Id = 3, DiagnosisId = 10, CreatedAt = at.AddMinutes(2), EventType = "STARTED" },
            new() { Id = 4, DiagnosisId = 10, CreatedAt = at.AddMinutes(3), EventType = "FINAL_APPROVED" },
            new() { Id = 5, DiagnosisId = 20, CreatedAt = at, EventType = "STARTED" }
        };
        var method = typeof(QuotationBuildService).GetMethod("SelectCurrentRecommendationScope", BindingFlags.NonPublic | BindingFlags.Static)!;
        var scope = ((List<long>, List<long>))method.Invoke(null, new object[] { logs, new List<long> { 10, 20, 30 } })!;
        Assert.Equal(new long[] { 2, 3, 4, 5 }, scope.Item1);
        Assert.Equal(new long[] { 20, 30 }, scope.Item2);
    }
}
