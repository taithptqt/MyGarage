using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MyGarage.Models;

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
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (pendingAudits.Count == 0)
        {
            return result;
        }

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

        var accessor = new HttpContextAccessor();
        var httpContext = accessor.HttpContext;
        var user = httpContext?.User;

        var userId = ParseLong(user?.FindFirstValue(ClaimTypes.NameIdentifier)) ?? 0;
        var userType = user?.FindFirstValue(ClaimTypes.Role) ?? "SYSTEM";
        var requestBranchId =
            ParseLong(user?.FindFirstValue("branchId")) ??
            ParseLong(user?.FindFirstValue("BranchId"));

        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        var sessionId = httpContext?.TraceIdentifier;
        var timestamp = DateTime.UtcNow;

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

            foreach (var property in entry.Properties)
            {
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

            if (entry.State == EntityState.Modified && pending.OldValues.Count == 0 && pending.NewValues.Count == 0)
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

