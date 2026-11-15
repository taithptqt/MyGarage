using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chat;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;
using MyGarage.Models;

namespace MyGarage.Services
{
    internal static class ChatV2OrderCardSupport
    {
        private const int MaxOrderCardsPerConversation = 30;
        internal static readonly JsonSerializerOptions OrderCardSerializerOptions = new(JsonSerializerDefaults.Web);

        internal static async Task<IReadOnlyList<ChatOrderCardDto>> BuildConversationOrderCardsAsync(
            IChatStore store,
            long customerId,
            bool forReceptionist,
            CancellationToken cancellationToken)
        {
            var retailCards = await BuildRetailOrderCardsAsync(store, customerId, forReceptionist, cancellationToken);
            var serviceCards = await BuildServiceOrderCardsAsync(store, customerId, forReceptionist, cancellationToken);

            return retailCards
                .Concat(serviceCards)
                .OrderByDescending(card => card.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(card => card.OrderId)
                .Take(MaxOrderCardsPerConversation)
                .ToList();
        }

        private static async Task<IReadOnlyList<ChatOrderCardDto>> BuildRetailOrderCardsAsync(
            IChatStore store,
            long customerId,
            bool forReceptionist,
            CancellationToken cancellationToken)
        {
            var orders = await store.GetCustomerRetailOrdersForChatAsync(customerId, MaxOrderCardsPerConversation, cancellationToken);

            var cards = new List<ChatOrderCardDto>(orders.Count);
            foreach (var order in orders)
            {
                if (!IsRetailOrderPurchased(order.Status))
                {
                    continue;
                }

                var details = order.RetailOrderItems
                    .OrderBy(item => item.Id)
                    .ToList();
                if (details.Count == 0)
                {
                    continue;
                }

                var rawItemCount = details.Sum(item => item.Quantity);
                var itemCount = rawItemCount <= 0
                    ? 1
                    : (int)Math.Max(
                        1,
                        Math.Min(int.MaxValue, Math.Round((double)rawItemCount, MidpointRounding.AwayFromZero)));

                var primaryItem = details
                    .OrderByDescending(item => item.FinalAmount)
                    .ThenBy(item => item.Id)
                    .FirstOrDefault();

                var imageUrl = primaryItem?.Product?.ProductImages?
                    .OrderByDescending(image => image.IsMain == true)
                    .ThenBy(image => image.SortOrder ?? int.MaxValue)
                    .ThenBy(image => image.Id)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault();

                var status = ChatV2MessagingSupport.NormalizeToken(order.Status);
                cards.Add(new ChatOrderCardDto
                {
                    OrderType = "RETAIL",
                    OrderId = order.Id,
                    OrderCode = string.IsNullOrWhiteSpace(order.OrderCode) ? $"RO-{order.Id}" : order.OrderCode.Trim(),
                    Title = ChatV2MessagingSupport.SafeText(primaryItem?.Product?.Name, $"Đơn phụ tùng #{order.Id}"),
                    Subtitle = $"{itemCount} san pham, Tong don: {ChatV2MessagingSupport.FormatCurrencyVnd(order.FinalAmount)}",
                    Status = status,
                    StatusLabel = ResolveOrderStatusLabel("RETAIL", status),
                    TotalAmount = order.FinalAmount,
                    ItemCount = itemCount,
                    ImageUrl = imageUrl,
                    BranchName = order.Branch?.Name,
                    CreatedAt = ChatV2MessagingSupport.ToChatDisplayTime(order.CreatedAt ?? order.UpdatedAt),
                    DetailUrl = forReceptionist ? null : $"/my-retail-orders?orderId={order.Id}"
                });
            }

            return cards;
        }

        private static async Task<IReadOnlyList<ChatOrderCardDto>> BuildServiceOrderCardsAsync(
            IChatStore store,
            long customerId,
            bool forReceptionist,
            CancellationToken cancellationToken)
        {
            var orders = await store.GetCustomerServiceOrdersForChatAsync(customerId, MaxOrderCardsPerConversation, cancellationToken);

            var productIds = orders
                .SelectMany(order => order.ServiceOrderDetails ?? Array.Empty<ServiceOrderDetail>())
                .Where(detail => IsServiceDetailProduct(detail) && detail.ItemId.HasValue)
                .Select(detail => detail.ItemId!.Value)
                .Distinct()
                .ToList();

            Dictionary<long, string?> imageByProductId = new();
            if (productIds.Count > 0)
            {
                var images = await store.GetProductImagesByProductIdsAsync(productIds, cancellationToken);

                imageByProductId = images
                    .GroupBy(image => image.ProductId)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .OrderByDescending(image => image.IsMain == true)
                            .ThenBy(image => image.SortOrder ?? int.MaxValue)
                            .ThenBy(image => image.Id)
                            .Select(image => image.ImageUrl)
                            .FirstOrDefault());
            }

            var cards = new List<ChatOrderCardDto>(orders.Count);
            foreach (var order in orders)
            {
                if (!IsServiceOrderDisplayable(order.Status))
                {
                    continue;
                }

                var details = order.ServiceOrderDetails
                    .OrderBy(detail => detail.Id)
                    .ToList();
                if (details.Count == 0)
                {
                    continue;
                }

                var primaryDetail = details
                    .OrderByDescending(detail => detail.FinalAmount)
                    .ThenBy(detail => detail.Id)
                    .First();

                string? imageUrl = null;
                if (IsServiceDetailProduct(primaryDetail)
                    && primaryDetail.ItemId.HasValue
                    && imageByProductId.TryGetValue(primaryDetail.ItemId.Value, out var mappedImage))
                {
                    imageUrl = mappedImage;
                }

                var totalAmount = order.FinalAmount ?? details.Sum(detail => detail.FinalAmount);
                var itemCount = Math.Max(1, details.Count);
                var status = ChatV2MessagingSupport.NormalizeToken(order.Status);
                cards.Add(new ChatOrderCardDto
                {
                    OrderType = "SERVICE",
                    OrderId = order.Id,
                    OrderCode = string.IsNullOrWhiteSpace(order.Code) ? $"SO-{order.Id}" : order.Code.Trim(),
                    Title = ChatV2MessagingSupport.SafeText(primaryDetail.ItemName, $"Đơn dịch vụ #{order.Id}"),
                    Subtitle = $"{itemCount} hang muc, Tong don: {ChatV2MessagingSupport.FormatCurrencyVnd(totalAmount)}",
                    Status = status,
                    StatusLabel = ResolveOrderStatusLabel("SERVICE", status),
                    TotalAmount = totalAmount,
                    ItemCount = itemCount,
                    ImageUrl = imageUrl,
                    BranchName = order.ExecuteBranch?.Name,
                    CreatedAt = ChatV2MessagingSupport.ToChatDisplayTime(order.CreatedAt ?? order.UpdatedAt),
                    DetailUrl = forReceptionist
                        ? $"/workshop/reception?viewOrderId={order.Id}"
                        : order.AppointmentId.HasValue
                            ? $"/my-appointments?appointmentId={order.AppointmentId.Value}"
                            : "/my-appointments"
                });
            }

            return cards;
        }

        private static bool IsRetailOrderPurchased(string? status)
        {
            var normalized = ChatV2MessagingSupport.NormalizeToken(status);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return true;
            }

            return normalized != "DRAFT"
                && normalized != "CANCELED";
        }

        private static bool IsServiceOrderDisplayable(string? status)
        {
            var normalized = ChatV2MessagingSupport.NormalizeToken(status);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return normalized == "READY"
                || normalized == "CLOSED"
                || normalized == "COMPLETED"
                || normalized == "WAITING_PAYMENT";
        }

        private static bool IsServiceDetailProduct(ServiceOrderDetail detail)
        {
            var type = ChatV2MessagingSupport.NormalizeToken(detail.ItemType);
            return type == "PRODUCT"
                || type == "PART"
                || type == "PHUTUNG"
                || type == "PHU_TUNG";
        }

        internal static string ResolveOrderStatusLabel(string orderType, string status)
        {
            var normalizedType = ChatV2MessagingSupport.NormalizeToken(orderType);
            var normalizedStatus = ChatV2MessagingSupport.NormalizeToken(status);

            if (normalizedType == "RETAIL")
            {
                return normalizedStatus switch
                {
                    "CONFIRMED" => "Da xac nhan",
                    "PAID" => "Da thanh toan",
                    "COMPLETED" => "Da hoan thanh",
                    "DRAFT" => "Cho thanh toan",
                    _ => string.IsNullOrWhiteSpace(normalizedStatus) ? "Đang xử lý" : normalizedStatus
                };
            }

            return normalizedStatus switch
            {
                "READY" => "San sang ban giao",
                "COMPLETED" => "Da hoan thanh",
                "CLOSED" => "Da dong",
                "WAITING_PAYMENT" => "Cho thanh toan",
                "REPAIRING" => "Dang sua chua",
                "WAITING_APPROVAL" => "Chờ báo giá",
                _ => string.IsNullOrWhiteSpace(normalizedStatus) ? "Đang xử lý" : normalizedStatus
            };
        }

        internal static ChatOrderCardDto NormalizeOrderCardRequest(ChatOrderCardRequestDto request)
        {
            if (request.OrderId <= 0)
            {
                throw new InvalidOperationException("OrderCard.OrderId must be greater than 0.");
            }

            var orderType = ChatV2MessagingSupport.NormalizeToken(request.OrderType);
            if (orderType != "RETAIL" && orderType != "SERVICE")
            {
                throw new InvalidOperationException("OrderCard.OrderType must be RETAIL or SERVICE.");
            }

            var orderCode = ChatV2MessagingSupport.SafeText(request.OrderCode, orderType == "RETAIL" ? $"RO-{request.OrderId}" : $"SO-{request.OrderId}");
            var title = ChatV2MessagingSupport.SafeText(request.Title, orderCode);
            var subtitle = ChatV2MessagingSupport.SafeText(request.Subtitle, "Thông tin đơn hàng");
            var status = ChatV2MessagingSupport.NormalizeToken(request.Status);
            var statusLabel = ChatV2MessagingSupport.SafeText(request.StatusLabel, ResolveOrderStatusLabel(orderType, status));
            var normalizedDetailUrl = ChatV2MessagingSupport.NormalizeDetailUrl(request.DetailUrl);

            return new ChatOrderCardDto
            {
                OrderType = orderType,
                OrderId = request.OrderId,
                OrderCode = orderCode,
                Title = title,
                Subtitle = subtitle,
                Status = status,
                StatusLabel = statusLabel,
                TotalAmount = request.TotalAmount,
                ItemCount = request.ItemCount,
                ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
                BranchName = string.IsNullOrWhiteSpace(request.BranchName) ? null : request.BranchName.Trim(),
                CreatedAt = request.CreatedAt,
                DetailUrl = normalizedDetailUrl
            };
        }

        internal static StoredOrderCardPayload ToStoredOrderCardPayload(ChatOrderCardDto orderCard)
        {
            return new StoredOrderCardPayload
            {
                OrderType = orderCard.OrderType,
                OrderId = orderCard.OrderId,
                OrderCode = orderCard.OrderCode,
                Title = orderCard.Title,
                Subtitle = orderCard.Subtitle,
                Status = orderCard.Status,
                StatusLabel = orderCard.StatusLabel,
                TotalAmount = orderCard.TotalAmount,
                ItemCount = orderCard.ItemCount,
                ImageUrl = orderCard.ImageUrl,
                BranchName = orderCard.BranchName,
                CreatedAt = orderCard.CreatedAt,
                DetailUrl = orderCard.DetailUrl
            };
        }

        internal static string BuildOrderCardConversationPreview(ChatOrderCardDto orderCard)
        {
            var title = ChatV2MessagingSupport.SafeText(orderCard.Title, orderCard.OrderCode);
            return $"[Đơn hàng] {title}";
        }

        internal static ChatOrderCardDto? TryParseStoredOrderCard(string messageType, string? content)
        {
            var isOrderCardType = string.Equals(
                messageType,
                nameof(ChatMessageType.ORDER_CARD),
                StringComparison.OrdinalIgnoreCase);

            if (!isOrderCardType
                && (
                    string.IsNullOrWhiteSpace(content)
                    || content.IndexOf("orderType", StringComparison.OrdinalIgnoreCase) < 0
                    || content.IndexOf("orderId", StringComparison.OrdinalIgnoreCase) < 0
                ))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<StoredOrderCardPayload>(content, OrderCardSerializerOptions);
                if (parsed is null || parsed.OrderId <= 0)
                {
                    return null;
                }

                var orderType = ChatV2MessagingSupport.NormalizeToken(parsed.OrderType);
                if (orderType != "RETAIL" && orderType != "SERVICE")
                {
                    return null;
                }

                var normalizedStatus = ChatV2MessagingSupport.NormalizeToken(parsed.Status);
                return new ChatOrderCardDto
                {
                    OrderType = orderType,
                    OrderId = parsed.OrderId,
                    OrderCode = ChatV2MessagingSupport.SafeText(parsed.OrderCode, orderType == "RETAIL" ? $"RO-{parsed.OrderId}" : $"SO-{parsed.OrderId}"),
                    Title = ChatV2MessagingSupport.SafeText(parsed.Title, "Đơn hàng"),
                    Subtitle = ChatV2MessagingSupport.SafeText(parsed.Subtitle, "Thông tin đơn hàng"),
                    Status = normalizedStatus,
                    StatusLabel = ChatV2MessagingSupport.SafeText(parsed.StatusLabel, ResolveOrderStatusLabel(orderType, normalizedStatus)),
                    TotalAmount = parsed.TotalAmount,
                    ItemCount = parsed.ItemCount,
                    ImageUrl = string.IsNullOrWhiteSpace(parsed.ImageUrl) ? null : parsed.ImageUrl.Trim(),
                    BranchName = string.IsNullOrWhiteSpace(parsed.BranchName) ? null : parsed.BranchName.Trim(),
                    CreatedAt = parsed.CreatedAt,
                    DetailUrl = ChatV2MessagingSupport.NormalizeDetailUrl(parsed.DetailUrl)
                };
            }
            catch
            {
                return null;
            }
        }

        internal sealed class StoredOrderCardPayload
        {
            public string? OrderType { get; set; }
            public long OrderId { get; set; }
            public string? OrderCode { get; set; }
            public string? Title { get; set; }
            public string? Subtitle { get; set; }
            public string? Status { get; set; }
            public string? StatusLabel { get; set; }
            public decimal? TotalAmount { get; set; }
            public int? ItemCount { get; set; }
            public string? ImageUrl { get; set; }
            public string? BranchName { get; set; }
            public DateTime? CreatedAt { get; set; }
            public string? DetailUrl { get; set; }
        }
    }
}
