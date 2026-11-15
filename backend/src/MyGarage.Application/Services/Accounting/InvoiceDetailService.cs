using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InvoiceDetailService : GenericService<InvoiceDetail>, IInvoiceDetailService
    {
        public InvoiceDetailService(IInvoiceDetailRepository repository) : base(repository)
        {
        }
    }
}
