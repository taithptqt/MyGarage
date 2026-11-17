using System.Collections.Generic;
using MyGarage.Application.Payments.DTOs;

namespace MyGarage.Interfaces
{
    public interface IOrderInvoiceStore
    {
        List<OrderInvoice> GetAllInvoices();
        OrderInvoice? GetInvoiceById(int id);
        OrderInvoice? GetInvoiceByInvoiceId(string invoiceId);
        List<OrderInvoice> GetInvoicesByOrderId(int orderId);
        List<OrderInvoice> GetInvoicesByOrderCode(long orderCode);
        List<OrderInvoice> GetInvoicesByPaymentLinkId(string paymentLinkId);
        void CreateInvoice(OrderInvoice invoice);
        void CreateInvoices(List<OrderInvoice> invoices);
        bool UpdateInvoice(int id, OrderInvoice updatedInvoice);
        bool DeleteInvoice(int id);
        bool DeleteInvoicesByOrderId(int orderId);
    }
}
