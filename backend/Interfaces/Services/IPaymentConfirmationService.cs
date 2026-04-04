using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IPaymentConfirmationService
    {
        Task ApplyOutcomeAsync(Payment payment, decimal amountPaid, string status);
    }
}
