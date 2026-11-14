using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Payroll;

namespace MyGarage.Infrastructure.Payroll;

public sealed class SalaryProfileListReader(SalaryProfileReadDbContext db) : ISalaryProfileListReader
{
    public async Task<SalaryProfileListResult> ReadAsync(long? branchId, int? page, int pageSize, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            int? total = null;
            var offset = page.HasValue ? ((long)page.Value - 1) * pageSize : 0;
            if (page.HasValue)
            {
                await using var count = connection.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM dbo.staff_salary_profiles WHERE (@branchId IS NULL OR branch_id = @branchId)";
                count.Parameters.Add(new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value));
                total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken));
                if (offset >= total) return new SalaryProfileListResult(Array.Empty<Dictionary<string, object?>>(), total);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT p.id, p.staff_id, s.FullName AS staff_name, p.branch_id, b.Name AS branch_name,
                    p.base_salary, p.effective_from, p.effective_to, p.status, p.note,
                    p.updated_by, p.created_at, p.updated_at
                FROM dbo.staff_salary_profiles p
                LEFT JOIN dbo.Staffs s ON s.Id = p.staff_id
                LEFT JOIN dbo.Branches b ON b.Id = p.branch_id
                WHERE (@branchId IS NULL OR p.branch_id = @branchId)
                ORDER BY p.created_at DESC, p.id DESC
                """;
            command.Parameters.Add(new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value));
            if (page.HasValue)
            {
                command.CommandText += " OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY";
                command.Parameters.Add(new SqlParameter("@offset", offset));
                command.Parameters.Add(new SqlParameter("@size", pageSize));
            }
            var rows = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var parts = reader.GetName(i).Split('_', StringSplitOptions.RemoveEmptyEntries);
                    var key = parts[0].ToLowerInvariant() + string.Concat(parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
                    var value = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
                    if (value is string text && !string.IsNullOrWhiteSpace(text) && text.IndexOfAny(['\u00C3', '\u00C2', '\u00D0', '\u00C4']) >= 0)
                    {
                        var decoded = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(text));
                        if (!decoded.Contains('\uFFFD')) value = decoded;
                    }
                    row[key] = value;
                }
                rows.Add(row);
            }
            return new SalaryProfileListResult(rows, total);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}
