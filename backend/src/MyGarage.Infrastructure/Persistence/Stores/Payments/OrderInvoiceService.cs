using MyGarage.Application.Payments.DTOs;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class OrderInvoiceService : IOrderInvoiceStore
{
    private readonly List<OrderInvoice> _invoices = [];
    private int _nextId = 1;

    public List<OrderInvoice> GetAllInvoices()
    {
        return _invoices;
    }

    public OrderInvoice? GetInvoiceById(int id)
    {
        return _invoices.FirstOrDefault(i => i.Id == id);
    }

    public OrderInvoice? GetInvoiceByInvoiceId(string invoiceId)
    {
        return _invoices.FirstOrDefault(i => i.InvoiceId == invoiceId);
    }

    public List<OrderInvoice> GetInvoicesByOrderId(int orderId)
    {
        return _invoices.Where(i => i.OrderId == orderId).ToList();
    }

    public List<OrderInvoice> GetInvoicesByOrderCode(long orderCode)
    {
        return _invoices.Where(i => i.OrderCode == orderCode).ToList();
    }

    public List<OrderInvoice> GetInvoicesByPaymentLinkId(string paymentLinkId)
    {
        return _invoices.Where(i => i.PaymentLinkId == paymentLinkId).ToList();
    }

    public void CreateInvoice(OrderInvoice invoice)
    {
        invoice.Id = _nextId++;
        _invoices.Add(invoice);
    }

    public void CreateInvoices(List<OrderInvoice> invoices)
    {
        foreach (var invoice in invoices)
        {
            CreateInvoice(invoice);
        }
    }

    public bool UpdateInvoice(int id, OrderInvoice updatedInvoice)
    {
        var invoice = _invoices.FirstOrDefault(i => i.Id == id);
        if (invoice == null) return false;

        invoice.OrderId = updatedInvoice.OrderId;
        invoice.OrderCode = updatedInvoice.OrderCode;
        invoice.PaymentLinkId = updatedInvoice.PaymentLinkId;
        invoice.InvoiceId = updatedInvoice.InvoiceId;
        invoice.InvoiceNumber = updatedInvoice.InvoiceNumber;
        invoice.IssuedTimestamp = updatedInvoice.IssuedTimestamp;
        invoice.IssuedDatetime = updatedInvoice.IssuedDatetime;
        invoice.TransactionId = updatedInvoice.TransactionId;
        invoice.ReservationCode = updatedInvoice.ReservationCode;
        invoice.CodeOfTax = updatedInvoice.CodeOfTax;

        return true;
    }

    public bool DeleteInvoice(int id)
    {
        var invoice = _invoices.FirstOrDefault(i => i.Id == id);
        if (invoice == null) return false;
        _invoices.Remove(invoice);
        return true;
    }

    public bool DeleteInvoicesByOrderId(int orderId)
    {
        var invoicesToRemove = _invoices.Where(i => i.OrderId == orderId).ToList();
        foreach (var invoice in invoicesToRemove)
        {
            _invoices.Remove(invoice);
        }
        return invoicesToRemove.Count > 0;
    }
}

