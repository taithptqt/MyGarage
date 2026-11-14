using Microsoft.EntityFrameworkCore;

namespace MyGarage.Infrastructure.Payroll;

// Read adapter only; the legacy GarageContext remains the schema and write owner.
public sealed class SalaryProfileReadDbContext(DbContextOptions<SalaryProfileReadDbContext> options) : DbContext(options)
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("This context is read-only.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This context is read-only.");
}
