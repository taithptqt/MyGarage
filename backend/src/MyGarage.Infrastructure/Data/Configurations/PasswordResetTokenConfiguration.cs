using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyGarage.Models;

namespace MyGarage.Data.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> entity)
    {
        entity.ToTable("PasswordResetTokens", tb =>
        {
            tb.HasCheckConstraint(
                "CK_PasswordResetTokens_Principal",
                "([CustomerId] IS NOT NULL AND [StaffId] IS NULL) OR ([CustomerId] IS NULL AND [StaffId] IS NOT NULL)");
            tb.HasCheckConstraint(
                "CK_PasswordResetTokens_TokenHash_NotEmpty",
                "LEN([TokenHash]) > 0");
        });

        entity.HasKey(e => e.Id).HasName("PK_PasswordResetTokens");

        entity.HasIndex(e => e.TokenHash, "UX_PasswordResetTokens_TokenHash").IsUnique();
        entity.HasIndex(e => new { e.CustomerId, e.ExpiresAt }, "IX_PasswordResetTokens_CustomerId_ExpiresAt");
        entity.HasIndex(e => new { e.StaffId, e.ExpiresAt }, "IX_PasswordResetTokens_StaffId_ExpiresAt");

        entity.Property(e => e.TokenHash)
            .HasMaxLength(64)
            .IsUnicode(false);

        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        // Both consumption and revocation must invalidate a stale reader.
        entity.Property(e => e.UsedAt).IsConcurrencyToken();
        entity.Property(e => e.RevokedAt).IsConcurrencyToken();

        entity.HasOne(d => d.Customer)
            .WithMany()
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PasswordResetTokens_Customers_CustomerId");

        entity.HasOne(d => d.Staff)
            .WithMany()
            .HasForeignKey(d => d.StaffId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PasswordResetTokens_Staffs_StaffId");
    }
}
