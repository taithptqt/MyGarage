using Microsoft.Extensions.DependencyInjection;
using MyGarage.Interfaces;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataSegregatedPortsTests
{
    [Fact]
    public async Task ResourcePorts_UseSameScopedQueryImplementation()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var aggregate = services.GetRequiredService<IODataQueryService>();
        Assert.Same(aggregate, services.GetRequiredService<IServiceOrderODataQuery>());
        Assert.Same(aggregate, services.GetRequiredService<IQuotationODataQuery>());
        Assert.Same(aggregate, services.GetRequiredService<IAuditLogODataQuery>());
        Assert.Same(aggregate, services.GetRequiredService<IVehicleODataQuery>());
        Assert.Same(aggregate, services.GetRequiredService<IAppointmentODataQuery>());
        Assert.Same(aggregate, services.GetRequiredService<IInventoryODataQuery>());
        Assert.Same(aggregate, services.GetRequiredService<IStaffODataQuery>());
        Assert.Same(aggregate, services.GetRequiredService<ICustomerODataQuery>());
    }
}