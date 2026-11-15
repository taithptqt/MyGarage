using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    public DbSet<CameraDevice> CameraDevices => Set<CameraDevice>();
    public DbSet<VehicleAccessEvent> VehicleAccessEvents => Set<VehicleAccessEvent>();
    public DbSet<VehicleAccessAudit> VehicleAccessAudits => Set<VehicleAccessAudit>();

    private static void ConfigureCameraAccess(ModelBuilder model)
    {
        model.Entity<CameraDevice>().HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<VehicleAccessEvent>().HasIndex(x => new { x.CameraId, x.EventKey }).IsUnique();
        model.Entity<VehicleAccessEvent>().HasIndex(x => new { x.BranchId, x.ReceivedAt });
        model.Entity<VehicleAccessEvent>().HasOne<CameraDevice>().WithMany().HasForeignKey(x => x.CameraId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<VehicleAccessEvent>().HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<VehicleAccessEvent>().HasOne<GatePass>().WithMany().HasForeignKey(x => x.GatePassId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<VehicleAccessEvent>().HasIndex(x => x.GatePassId).IsUnique().HasFilter("[GatePassId] IS NOT NULL AND [Status] = 'CONFIRMED'");
        model.Entity<VehicleAccessAudit>().HasIndex(x => new { x.BranchId, x.At });
    }
}
