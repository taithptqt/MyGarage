using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using MyGarage.DTOs.OData;
using MyGarage.Application.Ai;

namespace MyGarage.OData;

public static class MyGarageEdmModel
{
    public static IEdmModel Create()
    {
        var builder = new ODataConventionModelBuilder();

        var serviceOrders = builder.EntitySet<ServiceOrderODataDto>("ServiceOrders");
        serviceOrders.EntityType.HasKey(x => x.Id);
        serviceOrders.EntityType.Property(x => x.CustomerName).IsNotFilterable().IsNotSortable();
        serviceOrders.EntityType.Property(x => x.BranchName).IsNotFilterable().IsNotSortable();

        var quotations = builder.EntitySet<QuotationODataDto>("Quotations");
        quotations.EntityType.HasKey(x => x.Id);
        quotations.EntityType.Property(x => x.CustomerName).IsNotFilterable().IsNotSortable();

        var auditLogs = builder.EntitySet<AuditLogODataDto>("AuditLogs");
        auditLogs.EntityType.HasKey(x => x.Id);
        auditLogs.EntityType.Property(x => x.OldValue).IsNotFilterable().IsNotSortable();
        auditLogs.EntityType.Property(x => x.NewValue).IsNotFilterable().IsNotSortable();

        var vehicles = builder.EntitySet<VehicleODataDto>("Vehicles");
        vehicles.EntityType.HasKey(x => x.Id);
        vehicles.EntityType.Property(x => x.Vin).IsNotFilterable().IsNotSortable();
        vehicles.EntityType.Property(x => x.ModelName).IsNotSortable();

        var appointments = builder.EntitySet<AppointmentODataDto>("Appointments");
        appointments.EntityType.HasKey(x => x.Id);
        appointments.EntityType.Property(x => x.CustomerNotes).IsNotFilterable().IsNotSortable();
        appointments.EntityType.Property(x => x.BranchName).IsNotSortable();

        var inventory = builder.EntitySet<InventoryODataDto>("Inventory");
        inventory.EntityType.HasKey(x => x.Id);
        inventory.EntityType.Property(x => x.LocationBin).IsNotSortable();

        var knowledge = builder.EntitySet<AiKnowledgeCatalogRow>("AiKnowledgeCatalog");
        knowledge.EntityType.HasKey(x => x.Id);

        var invoices = builder.EntitySet<InvoiceODataDto>("Invoices");
        invoices.EntityType.HasKey(x => x.Id);

        var staffs = builder.EntitySet<StaffODataDto>("Staffs");
        staffs.EntityType.HasKey(x => x.Id);
        staffs.EntityType.Property(x => x.Phone).IsNotSortable();
        staffs.EntityType.Property(x => x.Email).IsNotSortable();

        var customers = builder.EntitySet<CustomerODataDto>("Customers");
        customers.EntityType.HasKey(x => x.Id);
        customers.EntityType.Property(x => x.Phone).IsNotSortable();
        customers.EntityType.Property(x => x.Email).IsNotSortable();

        return builder.GetEdmModel();
    }
}
