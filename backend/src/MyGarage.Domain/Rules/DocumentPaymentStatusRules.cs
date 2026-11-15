namespace MyGarage.Domain.Payments;

/// <summary>Existing payment-status calculation shared by invoices and service orders.</summary>
public static class DocumentPaymentStatusRules
{
    public static string Resolve(decimal paidAmount, decimal totalAmount)
    {
        if (totalAmount <= 0m) return "PAID";
        if (paidAmount <= 0m) return "UNPAID";
        return paidAmount < totalAmount ? "PARTIAL" : "PAID";
    }
}
