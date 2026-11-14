using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    public virtual DbSet<MaintenanceReminder> MaintenanceReminders { get; set; }

    private static void ConfigureCustomerLifecycleModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MaintenanceReminder>(entity =>
        {
            entity.ToTable("MaintenanceReminders");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.ScheduledAt, e.Status }, "IX_MaintenanceReminders_Scheduled_Status");
            entity.HasIndex(e => new { e.CustomerId, e.VehicleId, e.ReminderType, e.Status }, "IX_MaintenanceReminders_Dedupe");

            entity.Property(e => e.ReminderType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Channel).HasMaxLength(20).IsUnicode(false).HasDefaultValue("PUSH");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("PENDING");
            entity.Property(e => e.EstimatedServices).HasMaxLength(1000);
            entity.Property(e => e.CustomerResponse).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Customer)
                .WithMany()
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Vehicle)
                .WithMany()
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ServiceOrder)
                .WithMany()
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.ResultAppointment)
                .WithMany()
                .HasForeignKey(d => d.ResultAppointmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
