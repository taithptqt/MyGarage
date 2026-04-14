using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IPaymentConfirmationService
    {
        Task ApplyOutcomeAsync(Payment payment, decimal amountPaid, string status);
        Task EnsureInvoiceLinkedAsync(Payment payment);
        Task SendRetailOrderSuccessEmailAsync(
            long retailOrderId,
            decimal amountPaid,
            string transactionCode,
            string status,
            System.DateTimeOffset transactionTime,
            string? fallbackEmail = null,
            string? fallbackFullName = null);
    }
}
