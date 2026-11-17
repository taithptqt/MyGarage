using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;
using Xunit;

namespace MyGarage.Tests;

public sealed class SegregatedServicePortsTests
{
    [Fact]
    public async Task Ports_ResolveUsingSameScopedStoresAndAuditService()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        Assert.Same(services.GetRequiredService<IAppointmentStore>(), services.GetRequiredService<IAppointmentScheduleQueryStore>());
        Assert.Same(services.GetRequiredService<IAppointmentStore>(), services.GetRequiredService<IAppointmentCatalogStore>());
        Assert.Same(services.GetRequiredService<IQuotationWorkflowStore>(), services.GetRequiredService<IQuotationPortalStore>());
        Assert.Same(services.GetRequiredService<IQuotationWorkflowStore>(), services.GetRequiredService<IQuotationLineageQueryStore>());
        Assert.Same(services.GetRequiredService<IQuotationWorkflowStore>(), services.GetRequiredService<IQuotationApprovalOrderSyncStore>());
        Assert.Same(services.GetRequiredService<IQuotationWorkflowStore>(), services.GetRequiredService<IQuotationBuildCommandStore>());
        Assert.Same(services.GetRequiredService<IAuditLogService>(), services.GetRequiredService<IAuditLogReader>());
        Assert.Same(services.GetRequiredService<IAuditLogService>(), services.GetRequiredService<IAuditLogWriter>());
        Assert.NotNull(services.GetRequiredService<IAppointmentNotificationService>());
        Assert.NotNull(services.GetRequiredService<IDiagnosisApprovalCommandService>());
        Assert.NotNull(services.GetRequiredService<IQuotationPortalNotificationService>());
    }
}