using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Common;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfShiftClosingDataService(IGarageContext context) : IShiftClosingDataService
{
    public async Task<ShiftReconciliationResult> ReconcileShiftAsync(
        long id,
        decimal? actualBalance,
        decimal? totalCashOut,
        long? managerId,
        string? note,
        long? actorId,
        CancellationToken ct = default)
    {
        var shift = await context.ShiftClosings
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (shift == null)
        {
            return new ShiftReconciliationResult(Found: false, AlreadyClosed: false);
        }

        if (!string.IsNullOrWhiteSpace(shift.Status) &&
            string.Equals(shift.Status, "CLOSED", StringComparison.OrdinalIgnoreCase))
        {
            return new ShiftReconciliationResult(Found: true, AlreadyClosed: true);
        }

        var paymentRows = await (
            from payment in context.Payments.AsNoTracking()
            join method in context.PaymentMethods.AsNoTracking() on payment.MethodId equals method.Id
            where payment.ShiftClosingId == id &&
                  string.Equals(payment.Status ?? string.Empty, "COMPLETED", StringComparison.OrdinalIgnoreCase)
            select new
            {
                payment.Amount,
                MethodCode = (method.MethodCode ?? string.Empty).Trim().ToUpperInvariant(),
                MethodName = (method.MethodName ?? string.Empty).Trim().ToUpperInvariant()
            }
        ).ToListAsync(ct);

        decimal cashIn = 0m;
        decimal transferIn = 0m;
        decimal onlineIn = 0m;

        foreach (var row in paymentRows)
        {
            if (row.MethodCode.Contains("CASH", StringComparison.OrdinalIgnoreCase) ||
                row.MethodName.Contains("TIEN MAT", StringComparison.OrdinalIgnoreCase))
            {
                cashIn += row.Amount;
                continue;
            }

            if (row.MethodCode.Contains("BANK", StringComparison.OrdinalIgnoreCase) ||
                row.MethodCode.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase) ||
                row.MethodName.Contains("CHUYEN KHOAN", StringComparison.OrdinalIgnoreCase))
            {
                transferIn += row.Amount;
                continue;
            }

            onlineIn += row.Amount;
        }

        var openingBalance = shift.OpeningBalance;
        var resolvedTotalCashIn = cashIn;
        var resolvedTotalCashOut = totalCashOut ?? shift.TotalCashOut ?? 0m;
        var expectedBalance = openingBalance + resolvedTotalCashIn - resolvedTotalCashOut;
        var resolvedActualBalance = actualBalance ?? shift.ActualBalance ?? expectedBalance;
        var discrepancy = resolvedActualBalance - expectedBalance;
        var now = VietnamTime.Now;

        shift.TotalCashIn = resolvedTotalCashIn;
        shift.TotalCashOut = resolvedTotalCashOut;
        shift.ExpectedBalance = expectedBalance;
        shift.ActualBalance = resolvedActualBalance;
        shift.Discrepancy = discrepancy;
        shift.ClosedAt = now;
        shift.Status = Math.Abs(discrepancy) <= 1000m ? "CLOSED" : "RECONCILED_WITH_VARIANCE";
        shift.Note = BuildShiftNote(shift.Note, note, transferIn, onlineIn);
        shift.ManagerId = managerId ?? shift.ManagerId ?? actorId;

        await context.SaveChangesAsync(ct);

        return new ShiftReconciliationResult(
            Found: true,
            AlreadyClosed: false,
            ShiftId: shift.Id,
            Status: shift.Status,
            OpeningBalance: shift.OpeningBalance,
            TotalCashIn: shift.TotalCashIn,
            TransferIn: transferIn,
            OnlineIn: onlineIn,
            TotalCashOut: shift.TotalCashOut,
            ExpectedBalance: shift.ExpectedBalance,
            ActualBalance: shift.ActualBalance,
            Discrepancy: shift.Discrepancy,
            ClosedAt: shift.ClosedAt);
    }

    private static string BuildShiftNote(string? existing, string? note, decimal transferIn, decimal onlineIn)
    {
        var newSummary = $"Đối soát ca: chuyển khoản={transferIn:#,##0.##}; online={onlineIn:#,##0.##}";
        var extraNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (string.IsNullOrWhiteSpace(existing))
        {
            return string.IsNullOrWhiteSpace(extraNote) ? newSummary : $"{newSummary} | {extraNote}";
        }

        return string.IsNullOrWhiteSpace(extraNote)
            ? $"{existing.Trim()} | {newSummary}"
            : $"{existing.Trim()} | {newSummary} | {extraNote}";
    }
}
