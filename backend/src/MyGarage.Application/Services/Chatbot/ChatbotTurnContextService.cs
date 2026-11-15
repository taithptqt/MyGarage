using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotTurnContextService : IChatbotTurnContextService
{
    private static readonly Regex MarkdownBoldRegex = new(@"\*\*(?<value>[^*]{3,160})\*\*", RegexOptions.Compiled);
    private static readonly Regex MarkdownCodeRegex = new(@"`(?<value>[^`]{3,160})`", RegexOptions.Compiled);
    private static readonly Regex ServiceOfferRegex = new(
        @"(?:dich vu)\s+(?<value>.+?)\s+(?:cua)\s+(?:chung toi)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public string NormalizeForRouting(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return input.Trim().ToLowerInvariant();
    }

    public string NormalizeForSignalMatching(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var lowered = input.Trim().ToLowerInvariant().Replace('\u0111', 'd').Replace('\u0110', 'd');
        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var buffer = new List<char>(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            buffer.Add(char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ');
        }

        return string.Join(' ', new string(buffer.ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public string BuildContextualUserInput(
        string userInput,
        IReadOnlyList<ChatbotMessageResponseDto> history)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return userInput;
        }

        var normalizedInput = NormalizeForSignalMatching(userInput);
        if (HasPricingSignal(normalizedInput) || !IsShortCategoryReply(normalizedInput))
        {
            return userInput;
        }

        var lastAssistantContent = GetLatestMessageContent(history, nameof(ChatbotMessageRole.ASSISTANT));
        var lastUserContent = GetLatestMessageContent(history, nameof(ChatbotMessageRole.USER));

        var assistantAskedPriceFollowUp = !string.IsNullOrWhiteSpace(lastAssistantContent)
            && IsPricingFollowUpPrompt(NormalizeForRouting(lastAssistantContent));
        var assistantIsBookingStepPrompt = !string.IsNullOrWhiteSpace(lastAssistantContent)
            && IsBookingStepPrompt(NormalizeForRouting(lastAssistantContent));
        var previousUserWasPricing = !string.IsNullOrWhiteSpace(lastUserContent)
            && HasPricingSignal(NormalizeForRouting(lastUserContent));

        if (assistantIsBookingStepPrompt)
        {
            return userInput;
        }

        if (assistantAskedPriceFollowUp || previousUserWasPricing)
        {
            return $"báo giá {userInput}".Trim();
        }

        return userInput;
    }

    public string? TryBuildBookingOfferHandoffPrefill(
        string userInput,
        IReadOnlyList<ChatbotMessageResponseDto> history)
    {
        var normalizedInput = NormalizeForSignalMatching(userInput);
        if (!IsAffirmativeBookingAcceptance(normalizedInput))
        {
            return null;
        }

        var lastAssistantContent = GetLatestMessageContent(history, nameof(ChatbotMessageRole.ASSISTANT))?.Trim();
        if (string.IsNullOrWhiteSpace(lastAssistantContent) || !IsBookingOfferPrompt(lastAssistantContent))
        {
            return null;
        }

        var recentUserContext = FindLatestBookingContextUserMessage(history);
        var serviceHint = TryExtractSuggestedServiceHint(lastAssistantContent);

        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(recentUserContext))
        {
            segments.Add(recentUserContext.Trim());
        }

        if (segments.Count == 0 || !segments.Any(HasDirectBookingSignal))
        {
            segments.Insert(0, "dat lich");
        }

        if (!string.IsNullOrWhiteSpace(serviceHint)
            && !segments.Any(segment =>
                NormalizeForSignalMatching(segment).Contains(NormalizeForSignalMatching(serviceHint), StringComparison.Ordinal)))
        {
            segments.Add(serviceHint.Trim());
        }

        var prefill = string.Join(' ', segments.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)).Trim();
        return string.IsNullOrWhiteSpace(prefill) ? null : prefill;
    }

    public string? BuildGarageGreetingReply(string normalizedInput)
    {
        if (!IsSimpleGreetingOnly(normalizedInput))
        {
            return null;
        }

        return "Xin chào bạn, bạn đã đến với MyGarage. Mình có thể hỗ trợ các nhu cầu trong garage như tư vấn bảo dưỡng/sửa chữa, báo giá, đặt lịch, kiểm tra đơn dịch vụ, phụ tùng và bảo hành. Bạn cần hỗ trợ gì hôm nay?";
    }

    private static string? GetLatestMessageContent(
        IReadOnlyList<ChatbotMessageResponseDto>? history,
        string role)
    {
        return history?
            .Where(x =>
                x is not null
                && string.Equals(x.Role, role, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(x.Content))
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Content!)
            .LastOrDefault();
    }

    private static bool HasPricingSignal(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        return normalizedInput.Contains("báo giá", StringComparison.Ordinal)
            || normalizedInput.Contains("bao gia", StringComparison.Ordinal)
            || normalizedInput.Contains("b?ng giá", StringComparison.Ordinal)
            || normalizedInput.Contains("bang gia", StringComparison.Ordinal)
            || normalizedInput.Contains("price", StringComparison.Ordinal)
            || normalizedInput.Contains("quote", StringComparison.Ordinal)
            || normalizedInput.Contains("quotation", StringComparison.Ordinal)
            || normalizedInput.Contains("chi phí", StringComparison.Ordinal)
            || normalizedInput.Contains("chi phi", StringComparison.Ordinal);
    }

    private static bool IsShortCategoryReply(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        var tokenCount = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return tokenCount is >= 1 and <= 4;
    }

    private static bool IsPricingFollowUpPrompt(string normalizedAssistantReply)
    {
        if (string.IsNullOrWhiteSpace(normalizedAssistantReply))
        {
            return false;
        }

        var asksGroupSelection = normalizedAssistantReply.Contains("nhóm nào", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("nhom nao", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("d?ch v? nào", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("dich vu nao", StringComparison.Ordinal);
        var hasPricingContext = HasPricingSignal(normalizedAssistantReply)
            || normalizedAssistantReply.Contains("g?n dúng", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("gan dung", StringComparison.Ordinal);

        return asksGroupSelection && hasPricingContext;
    }

    private static bool IsBookingStepPrompt(string normalizedAssistantReply)
    {
        if (string.IsNullOrWhiteSpace(normalizedAssistantReply))
        {
            return false;
        }

        return normalizedAssistantReply.Contains("buoc 1/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bu?c 1/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("buoc 2/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bu?c 2/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("buoc 3/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bu?c 3/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("buoc 4/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bu?c 4/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("buoc 5/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bu?c 5/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("chon xe", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("ch?n xe", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("chon nhom dich vu", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("ch?n nhóm d?ch v?", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("chon dich vu chi tiet", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("ch?n d?ch v? chi ti?t", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("chon chi nhanh", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("ch?n chi nhánh", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("chon khung gio", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("ch?n khung gi?", StringComparison.Ordinal);
    }

    private bool IsBookingOfferPrompt(string normalizedAssistantReply)
    {
        normalizedAssistantReply = NormalizeForSignalMatching(normalizedAssistantReply);
        if (string.IsNullOrWhiteSpace(normalizedAssistantReply))
        {
            return false;
        }

        var asksToBook = normalizedAssistantReply.Contains("ban co muon dat lich", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("muon dat lich cho dich vu nay", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("co muon hen lich", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("dat lich hen", StringComparison.Ordinal);
        if (!asksToBook)
        {
            return false;
        }

        return normalizedAssistantReply.Contains("dich vu", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bao duong", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("sua chua", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("dieu hoa", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("kiem tra", StringComparison.Ordinal);
    }

    private bool IsAffirmativeBookingAcceptance(string normalizedInput)
    {
        normalizedInput = NormalizeForSignalMatching(normalizedInput);
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        return normalizedInput.Equals("ok", StringComparison.Ordinal)
            || normalizedInput.Equals("oke", StringComparison.Ordinal)
            || normalizedInput.Equals("okay", StringComparison.Ordinal)
            || normalizedInput.Equals("yes", StringComparison.Ordinal)
            || normalizedInput.Equals("dong y", StringComparison.Ordinal)
            || normalizedInput.Equals("duoc", StringComparison.Ordinal)
            || normalizedInput.Equals("vang", StringComparison.Ordinal)
            || normalizedInput.Contains("dat lich", StringComparison.Ordinal)
            || normalizedInput.Contains("hen lich", StringComparison.Ordinal);
    }

    private string? FindLatestBookingContextUserMessage(IReadOnlyList<ChatbotMessageResponseDto>? history)
    {
        return history?
            .Where(x =>
                x is not null
                && string.Equals(x.Role, nameof(ChatbotMessageRole.USER), StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(x.Content))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Content!.Trim())
            .FirstOrDefault(HasBookingContextSignal);
    }

    private bool HasBookingContextSignal(string normalizedMessage)
    {
        normalizedMessage = NormalizeForSignalMatching(normalizedMessage);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return false;
        }

        if (HasDirectBookingSignal(normalizedMessage))
        {
            return true;
        }

        return normalizedMessage.Contains("ngay mai", StringComparison.Ordinal)
            || normalizedMessage.Contains("hom nay", StringComparison.Ordinal)
            || normalizedMessage.Contains("gio", StringComparison.Ordinal)
            || normalizedMessage.Contains("chi nhanh", StringComparison.Ordinal)
            || normalizedMessage.Contains("xe", StringComparison.Ordinal)
            || normalizedMessage.Contains("bien so", StringComparison.Ordinal)
            || normalizedMessage.Contains("slot", StringComparison.Ordinal)
            || Regex.IsMatch(normalizedMessage, @"\b\d{1,2}\s*h\b", RegexOptions.IgnoreCase);
    }

    private bool HasDirectBookingSignal(string normalizedMessage)
    {
        normalizedMessage = NormalizeForSignalMatching(normalizedMessage);
        return normalizedMessage.Contains("dat lich", StringComparison.Ordinal)
            || normalizedMessage.Contains("hen lich", StringComparison.Ordinal)
            || normalizedMessage.Contains("book", StringComparison.Ordinal)
            || normalizedMessage.Contains("appointment", StringComparison.Ordinal);
    }

    private static string? TryExtractSuggestedServiceHint(string assistantReply)
    {
        if (string.IsNullOrWhiteSpace(assistantReply))
        {
            return null;
        }

        foreach (Match match in MarkdownBoldRegex.Matches(assistantReply))
        {
            var value = match.Groups["value"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        foreach (Match match in MarkdownCodeRegex.Matches(assistantReply))
        {
            var value = match.Groups["value"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        var serviceMatch = ServiceOfferRegex.Match(assistantReply);
        if (serviceMatch.Success)
        {
            var value = serviceMatch.Groups["value"].Value
                .Replace("**", string.Empty, StringComparison.Ordinal)
                .Replace("`", string.Empty, StringComparison.Ordinal)
                .Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static bool IsSimpleGreetingOnly(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        var hasGreetingPrefix = normalizedInput.StartsWith("hi", StringComparison.Ordinal)
            || normalizedInput.StartsWith("hello", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chào", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chào", StringComparison.Ordinal)
            || normalizedInput.StartsWith("alo", StringComparison.Ordinal);
        if (!hasGreetingPrefix)
        {
            return false;
        }

        var hasTransactionalSignal =
            normalizedInput.Contains("đặt lịch", StringComparison.Ordinal)
            || normalizedInput.Contains("dat lich", StringComparison.Ordinal)
            || normalizedInput.Contains("bao gia", StringComparison.Ordinal)
            || normalizedInput.Contains("báo giá", StringComparison.Ordinal)
            || normalizedInput.Contains("thay dau", StringComparison.Ordinal)
            || normalizedInput.Contains("thay dầu", StringComparison.Ordinal)
            || normalizedInput.Contains("dich vu", StringComparison.Ordinal)
            || normalizedInput.Contains("dịch vụ", StringComparison.Ordinal);
        if (hasTransactionalSignal)
        {
            return false;
        }

        var tokenCount = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return tokenCount <= 6;
    }
}
