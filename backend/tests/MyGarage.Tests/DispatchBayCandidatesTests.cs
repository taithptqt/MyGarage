using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class DispatchBayCandidatesTests
{
    [Fact]
    public async Task Candidates_AreScopedOrderedAndBounded()
    {
        var options = new DbContextOptionsBuilder<GarageContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GarageContext(options, new HttpContextAccessor());
        Bay BayRow(long id, long branch, int sequence, string status = "AVAILABLE", bool active = true, bool deleted = false)
            => new() { Id = id, BranchId = branch, Code = $"B{id}", Name = $"Bay {id}", BayType = "GENERAL", ServiceArea = "WORKSHOP",
                SequenceOrder = sequence, OperationalStatus = status, IsActive = active, IsDeleted = deleted };
        db.Bays.AddRange(BayRow(1, 1, 5), BayRow(2, 1, 1), BayRow(3, 1, 2), BayRow(4, 1, 3),
            BayRow(5, 2, 0), BayRow(6, 1, 0, "OCCUPIED"), BayRow(7, 1, 0, active: false), BayRow(8, 1, 0, deleted: true));
        await db.SaveChangesAsync();
        var result = await new DispatchBayCandidates(db).GetAvailableAsync(1);
        Assert.Equal(new long[] { 2, 3, 4 }, result.Select(x => x.Id));
        Assert.Empty(await new DispatchBayCandidates(db).GetAvailableAsync(3));
    }
}
