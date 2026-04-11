using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public interface IChatbotPricingInventoryGuard
{
    Task<ChatbotPricingInventoryResult> ResolveAsync(
        long customerId,
        long? branchId,
        string productHint,
        CancellationToken cancellationToken = default);
}

public class ChatbotPricingInventoryResult
{
    public bool ProductFound { get; set; }

    public string ProductHint { get; set; } = string.Empty;

    public long? ProductId { get; set; }

    public string? ProductName { get; set; }

    public string? ProductSku { get; set; }

    public bool PriceAvailable { get; set; }

    public decimal? Price { get; set; }

    public string PriceSource { get; set; } = "UNAVAILABLE";

    public bool StockAvailable { get; set; }

    public decimal? StockQuantity { get; set; }

    public string StockSource { get; set; } = "UNAVAILABLE";
}
