using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Common.Pagination;
using MyGarage.Data;
using MyGarage.Extensions;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class PaginationUnitTests
{
    [Fact]
    public void NormalizePagination_NullInputs_ReturnsDefaultPageAndPageSize()
    {
        var (page, pageSize) = PaginationExtensions.NormalizePagination(null, null);

        Assert.Equal(1, page);
        Assert.Equal(50, pageSize);
    }

    [Theory]
    [InlineData(0, 50, 1, 50)]
    [InlineData(-10, 20, 1, 20)]
    [InlineData(-999, 10, 1, 10)]
    public void NormalizePagination_NonPositivePage_ClampsToOne(
        int? inputPage, int? inputPageSize, int expectedPage, int expectedPageSize)
    {
        var (page, pageSize) = PaginationExtensions.NormalizePagination(inputPage, inputPageSize);

        Assert.Equal(expectedPage, page);
        Assert.Equal(expectedPageSize, pageSize);
    }

    [Theory]
    [InlineData(1, 0, 1, 50)]
    [InlineData(1, -5, 1, 50)]
    [InlineData(2, null, 2, 50)]
    public void NormalizePagination_NonPositiveOrNullPageSize_FallsBackToDefault(
        int? inputPage, int? inputPageSize, int expectedPage, int expectedPageSize)
    {
        var (page, pageSize) = PaginationExtensions.NormalizePagination(inputPage, inputPageSize);

        Assert.Equal(expectedPage, page);
        Assert.Equal(expectedPageSize, pageSize);
    }

    [Fact]
    public void NormalizePagination_ExceedingMaxPageSize_ClampsToMax()
    {
        var (page, pageSize) = PaginationExtensions.NormalizePagination(1, 500, defaultPageSize: 50, maxPageSize: 100);

        Assert.Equal(1, page);
        Assert.Equal(100, pageSize);
    }

    [Fact]
    public void CreatePagedEnvelope_CalculatesTotalPagesAndDoesNotEmitHeaders()
    {
        var httpContext = new DefaultHttpContext();
        var response = httpContext.Response;

        // 105 items with page size 20 => 6 total pages
        var envelope = PaginationExtensions.CreatePagedEnvelope(
            new[] { "A", "B" },
            totalCount: 105,
            page: 2,
            pageSize: 20);

        Assert.Equal(105, envelope.TotalCount);
        Assert.Equal(105, envelope.Total);
        Assert.Equal(2, envelope.Page);
        Assert.Equal(20, envelope.PageSize);
        Assert.Equal(6, envelope.TotalPages);
        Assert.True(envelope.HasPreviousPage);
        Assert.True(envelope.HasNextPage);

        Assert.False(response.Headers.ContainsKey("X-Total-Count"));
        Assert.False(response.Headers.ContainsKey("X-Page"));
        Assert.False(response.Headers.ContainsKey("X-Page-Size"));
        Assert.False(response.Headers.ContainsKey("X-Total-Pages"));
    }

    [Fact]
    public void CreatePagedEnvelope_ZeroTotalCount_SetsTotalPagesToZero()
    {
        var httpContext = new DefaultHttpContext();
        var response = httpContext.Response;

        var envelope = PaginationExtensions.CreatePagedEnvelope(
            Array.Empty<string>(),
            totalCount: 0,
            page: 1,
            pageSize: 50);

        Assert.Equal(0, envelope.TotalCount);
        Assert.Equal(1, envelope.Page);
        Assert.Equal(50, envelope.PageSize);
        Assert.Equal(0, envelope.TotalPages);
        Assert.False(envelope.HasPreviousPage);
        Assert.False(envelope.HasNextPage);
        Assert.False(response.Headers.ContainsKey("X-Total-Count"));
    }

    [Fact]
    public void CreatePagedEnvelope_NullItems_DoesNotThrow()
    {
        IEnumerable<int>? nullItems = null;
        var exception = Record.Exception(() => PaginationExtensions.CreatePagedEnvelope(nullItems, 100, 1, 50));
        Assert.Null(exception);
    }

    [Fact]
    public void ToPagedEnvelope_InMemoryList_ReturnsCorrectSliceAndMetadata()
    {
        var items = Enumerable.Range(1, 150).Select(i => $"Item_{i}").ToList();

        // Request page 2 with page size 50 => items 51 to 100
        var envelope = items.ToPagedEnvelope(2, 50);

        Assert.Equal(50, envelope.Items.Count);
        Assert.Equal("Item_51", envelope.Items.First());
        Assert.Equal("Item_100", envelope.Items.Last());
        Assert.Equal(150, envelope.TotalCount);
        Assert.Equal(2, envelope.Page);
        Assert.Equal(50, envelope.PageSize);
        Assert.Equal(3, envelope.TotalPages);
        Assert.True(envelope.HasPreviousPage);
        Assert.True(envelope.HasNextPage);
    }

    [Fact]
    public void ToPagedEnvelope_ExtremelyLargePage_DoesNotWrapToFirstPage()
    {
        var envelope = Enumerable.Range(1, 100).ToPagedEnvelope(int.MaxValue, 100);
        Assert.Empty(envelope.Items);
        Assert.Equal(100, envelope.TotalCount);
    }

    [Fact]
    public void ToPagedEnvelope_NullSource_ReturnsEmptyEnvelope()
    {
        List<string>? nullList = null;

        var envelope = nullList.ToPagedEnvelope(1, 50);

        Assert.NotNull(envelope);
        Assert.Empty(envelope.Items);
        Assert.Equal(0, envelope.TotalCount);
        Assert.Equal(0, envelope.TotalPages);
    }

    [Fact]
    public async Task ToPagedEnvelopeAsync_Queryable_ReturnsCorrectSliceAndMetadata()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            for (int i = 1; i <= 35; i++)
            {
                db.VehicleBrands.Add(new VehicleBrand
                {
                    Id = 1000 + i,
                    Code = $"UB{i:D3}",
                    Name = $"UnitBrand_{i:D3}",
                    IsActive = true
                });
            }
            return Task.CompletedTask;
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();

        var query = db.VehicleBrands
            .AsNoTracking()
            .Where(b => b.Id > 1000)
            .OrderBy(b => b.Id);

        // Request page 2 with pageSize 10 => items 11 through 20
        var pagedBrands = await query.ToPagedEnvelopeAsync(2, 10);

        Assert.Equal(10, pagedBrands.Items.Count);
        Assert.Equal("UnitBrand_011", pagedBrands.Items.First().Name);
        Assert.Equal("UnitBrand_020", pagedBrands.Items.Last().Name);

        Assert.Equal(35, pagedBrands.TotalCount);
        Assert.Equal(2, pagedBrands.Page);
        Assert.Equal(10, pagedBrands.PageSize);
        Assert.Equal(4, pagedBrands.TotalPages);
        Assert.True(pagedBrands.HasPreviousPage);
        Assert.True(pagedBrands.HasNextPage);
    }
}
