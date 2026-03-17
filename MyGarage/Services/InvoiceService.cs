using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InvoiceService : GenericService<Invoice>, IInvoiceService
    {
        public InvoiceService(IInvoiceRepository repository) : base(repository)
        {
        }
    }
}
