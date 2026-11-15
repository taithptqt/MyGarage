using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MyGarage.Models;
using MyGarage.Domain.Rules;

namespace MyGarage.Data;

public partial class GarageContext
{
    private bool _isWritingAuditLogs;

    public override int SaveChanges()
    {
        return SaveChangesAsync().GetAwaiter().GetResult();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(true, cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (_isWritingAuditLogs)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        var pendingAudits = PrepareAuditEntries();
        if (pendingAudits.Count == 0)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        if (Database.IsRelational() && Database.CurrentTransaction == null)
        {
            var strategy = Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await Database.BeginTransactionAsync(cancellationToken);
                var result = await SaveWithAuditAsync(pendingAudits, acceptAllChangesOnSuccess, cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return result;
            });
        }

        return await SaveWithAuditAsync(pendingAudits, acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task<int> SaveWithAuditAsync(
        List<PendingAuditEntry> pendingAudits,
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken)
    {
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        try
        {
            _isWritingAuditLogs = true;
            foreach (var pending in pendingAudits)
            {
                pending.ResolveRecordId();
                AuditLogs.Add(pending.ToAuditLog());
            }

            await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        finally
        {
            _isWritingAuditLogs = false;
        }

        return result;
    }

    private List<PendingAuditEntry> PrepareAuditEntries()
    {
        ChangeTracker.DetectChanges();

        var actor = _auditContextAccessor?.GetCurrentContext();

        var userId = actor?.UserId ?? 0;
        var userType = NormalizeAuditUserType(actor?.Role);
        var requestBranchId = actor?.BranchId;

        var ipAddress = actor?.IpAddress;
        var userAgent = actor?.UserAgent;
        var sessionId = actor?.TraceId;
        var timestamp = VietnamTime.Now;

        var result = new List<PendingAuditEntry>();
        var entries = ChangeTracker.Entries()
            .Where(e =>
                e.State == EntityState.Added ||
                e.State == EntityState.Modified ||
                e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is AuditLog) continue;
            if (entry.Metadata.IsOwned()) continue;

            var tableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;
            if (string.Equals(tableName, "AuditLogs", StringComparison.OrdinalIgnoreCase)) continue;

            var branchId = requestBranchId ?? TryGetBranchId(entry);
            var pending = new PendingAuditEntry(
                entry,
                userId,
                userType,
                branchId,
                ipAddress,
                userAgent,
                sessionId,
                tableName,
                timestamp);

            var hasSensitiveChange = false;
            foreach (var property in entry.Properties)
            {
                if (entry.State == EntityState.Modified && property.IsModified &&
                    SharedOperationsDomainRules.IsSensitiveAuditField(property.Metadata.Name) &&
                    !Equals(property.OriginalValue, property.CurrentValue))
                {
                    hasSensitiveChange = true;
                }
                if (!IsAuditableProperty(property)) continue;

                var propertyName = property.Metadata.Name;
                if (entry.State == EntityState.Added)
                {
                    pending.NewValues[propertyName] = property.CurrentValue;
                    continue;
                }

                if (entry.State == EntityState.Deleted)
                {
                    pending.OldValues[propertyName] = property.OriginalValue;
                    continue;
                }

                if (entry.State == EntityState.Modified)
                {
                    if (!property.IsModified) continue;
                    if (Equals(property.OriginalValue, property.CurrentValue)) continue;

                    pending.OldValues[propertyName] = property.OriginalValue;
                    pending.NewValues[propertyName] = property.CurrentValue;
                }
            }

            if (entry.State == EntityState.Modified && pending.OldValues.Count == 0 &&
                pending.NewValues.Count == 0 && !hasSensitiveChange)
            {
                continue;
            }

            result.Add(pending);
        }

        return result;
    }

    private static bool IsAuditableProperty(PropertyEntry propertyEntry)
    {
        if (propertyEntry.Metadata.IsShadowProperty()) return false;
        if (propertyEntry.Metadata.IsConcurrencyToken) return false;
        if (SharedOperationsDomainRules.IsSensitiveAuditField(propertyEntry.Metadata.Name)) return false;

        var type = Nullable.GetUnderlyingType(propertyEntry.Metadata.ClrType) ?? propertyEntry.Metadata.ClrType;
        if (type.IsEnum) return true;
        if (type.IsPrimitive) return true;

        return type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(Guid)
            || type == typeof(TimeSpan);
    }

    private static long? ParseLong(string? value)
    {
        return long.TryParse(value, out var result) ? result : null;
    }

    private static string NormalizeAuditUserType(string? roleClaim)
    {
        if (string.IsNullOrWhiteSpace(roleClaim))
        {
            return "STAFF";
        }

        var normalized = roleClaim.Trim().ToUpperInvariant();
        return normalized.Contains("CUSTOMER", StringComparison.Ordinal) ? "CUSTOMER" : "STAFF";
    }

    private static long? TryGetBranchId(EntityEntry entry)
    {
        var branchProperty = entry.Properties
            .FirstOrDefault(p => string.Equals(p.Metadata.Name, "BranchId", StringComparison.OrdinalIgnoreCase));
        if (branchProperty == null) return null;
        return ParseLong(branchProperty.CurrentValue?.ToString());
    }

    private sealed class PendingAuditEntry
    {
        private readonly EntityEntry _entry;
        private readonly DateTime _timestamp;

        public PendingAuditEntry(
            EntityEntry entry,
            long userId,
            string userType,
            long? branchId,
            string? ipAddress,
            string? userAgent,
            string? sessionId,
            string tableName,
            DateTime timestamp)
        {
            _entry = entry;
            UserId = userId;
            UserType = userType;
            BranchId = branchId;
            IpAddress = ipAddress;
            UserAgent = userAgent;
            SessionId = sessionId;
            TableName = tableName;
            _timestamp = timestamp;
            Action = entry.State switch
            {
                EntityState.Added => "CREATE",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted => "DELETE",
                _ => "UNKNOWN"
            };
        }

        public long UserId { get; }
        public string UserType { get; }
        public long? BranchId { get; }
        public string Action { get; }
        public string TableName { get; }
        public string? IpAddress { get; }
        public string? UserAgent { get; }
        public string? SessionId { get; }
        public Dictionary<string, object?> OldValues { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, object?> NewValues { get; } = new(StringComparer.OrdinalIgnoreCase);
        public long RecordId { get; private set; }

        public void ResolveRecordId()
        {
            var key = _entry.Metadata.FindPrimaryKey();
            if (key == null || key.Properties.Count == 0)
            {
                RecordId = 0;
                return;
            }

            if (key.Properties.Count == 1)
            {
                var keyName = key.Properties[0].Name;
                var value = _entry.Property(keyName).CurrentValue ?? _entry.Property(keyName).OriginalValue;
                RecordId = ParseLong(value?.ToString()) ?? 0;
                if (RecordId > 0)
                {
                    NewValues[keyName] = RecordId;
                }
                return;
            }

            // Composite keys are serialized into NewValue/OldValue; RecordId keeps 0.
            RecordId = 0;
        }

        public AuditLog ToAuditLog()
        {
            return new AuditLog
            {
                UserId = UserId,
                UserType = UserType,
                BranchId = BranchId,
                Action = Action,
                TableName = TableName,
                RecordId = RecordId,
                OldValue = OldValues.Count > 0 ? JsonSerializer.Serialize(OldValues) : null,
                NewValue = NewValues.Count > 0 ? JsonSerializer.Serialize(NewValues) : null,
                IpAddress = IpAddress,
                UserAgent = UserAgent,
                SessionId = SessionId,
                Timestamp = _timestamp
            };
        }
    }
}

