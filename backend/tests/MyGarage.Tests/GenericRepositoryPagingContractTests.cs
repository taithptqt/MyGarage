using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Repositories;
using Xunit;

namespace MyGarage.Tests;

public sealed class GenericRepositoryPagingContractTests
{
    private static GarageContext CreateContext() => new(new DbContextOptionsBuilder<GarageContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task CompositeKeyPaging_OrdersByBothKeyPartsAndCountsAllRows()
    {
        await using var db = CreateContext();
        db.ModelSpecificRules.AddRange(
            new ModelSpecificRule { ModelId = 2, ProductId = 1 },
            new ModelSpecificRule { ModelId = 1, ProductId = 3 },
            new ModelSpecificRule { ModelId = 1, ProductId = 1 });
        await db.SaveChangesAsync();
        var repository = new ModelSpecificRuleRepository(db);

        var first = await repository.GetPageByIdAsync(1, 2);
        var second = await repository.GetPageByIdAsync(2, 2);
        Assert.Equal(3, first.Total);
        Assert.Equal(3, second.Total);
        Assert.Equal(new[] { (1L, 1L), (1L, 3L) }, first.Items.Select(x => (x.ModelId, x.ProductId)));
        Assert.Equal(new[] { (2L, 1L) }, second.Items.Select(x => (x.ModelId, x.ProductId)));
        Assert.Empty((await repository.GetPageByIdAsync(int.MaxValue, 100)).Items);
    }

    [Fact]
    public async Task StandardServicePaging_UsesSameVisibilityAsGetAll()
    {
        await using var db = CreateContext();
        db.StandardServices.AddRange(
            new StandardService { Id = 1, Code = "S1", Name = "Visible", IsActive = true },
            new StandardService { Id = 2, Code = "S2", Name = "Deleted", IsDeleted = true },
            new StandardService { Id = 3, Code = "S3", Name = "Inactive", IsActive = false },
            new StandardService { Id = 4, Code = "PKG-1", Name = "Package" },
            new StandardService { Id = 5, Code = "S5", Name = "Visible" });
        await db.SaveChangesAsync();
        var repository = new StandardServiceRepository(db);
        var all = (await repository.GetAllAsync()).Select(x => x.Id).Order().ToArray();
        var first = await repository.GetPageByIdAsync(1, 1);
        var second = await repository.GetPageByIdAsync(2, 1);
        Assert.Equal(new long[] { 1, 5 }, all);
        Assert.Equal(2, first.Total);
        Assert.Equal(2, second.Total);
        Assert.Equal(all, first.Items.Concat(second.Items).Select(x => x.Id));
    }

    [Fact]
    public async Task VehiclePaging_UsesSameVisibilityAsGetAll()
    {
        await using var db = CreateContext();
        db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "B1", Name = "Brand" });
        db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "Model" });
        db.Vehicles.AddRange(
            new Vehicle { Id = 3, LicensePlate = "V3", ModelId = 1 },
            new Vehicle { Id = 1, LicensePlate = "V1", ModelId = 1 },
            new Vehicle { Id = 2, LicensePlate = "V2", ModelId = 1, IsDeleted = true });
        await db.SaveChangesAsync();
        var repository = new VehicleRepository(db);
        var all = (await repository.GetAllAsync()).Select(x => x.Id).Order().ToArray();
        var first = await repository.GetPageByIdAsync(1, 1);
        var second = await repository.GetPageByIdAsync(2, 1);
        Assert.Equal(new long[] { 1, 3 }, all);
        Assert.Equal(2, first.Total);
        Assert.Equal(2, second.Total);
        Assert.Equal(all, first.Items.Concat(second.Items).Select(x => x.Id));
    }
}