using System;
using System.Linq;
using System.Text.Json;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Domain.Rules;

public static class DiagnosisRecommendationDomainRules
{
    public const string EvidenceRequiredMessage = "Mỗi dịch vụ mới phải có ít nhất 1 ảnh hiện trạng trước khi lưu.";
    public const string StatusActive = nameof(RecommendationLifecycleStatusCode.ACTIVE);
    public const string StatusSoftDeletedByTeamLeader = nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL);

    public static bool RequiresEvidenceForServiceRecommendation(DiagnosisRecommendation? recommendation)
    {
        if (recommendation == null)
        {
            return false;
        }

        return RequiresEvidenceForServiceRecommendation(
            recommendation.ParentRecommendationId,
            recommendation.StandardServiceId,
            recommendation.ItemType);
    }

    public static bool RequiresEvidenceForServiceRecommendation(
        long? parentRecommendationId,
        long? standardServiceId,
        string? itemType)
    {
        if (parentRecommendationId.HasValue && parentRecommendationId.Value > 0)
        {
            return false;
        }

        if (standardServiceId.HasValue && standardServiceId.Value > 0)
        {
            return true;
        }

        var normalizedItemType = (itemType ?? string.Empty).Trim().ToUpperInvariant();
        return normalizedItemType is "SERVICE" or "STANDARD_SERVICE" or "LABOR";
    }

    public static bool HasEvidenceMedia(string? evidenceMediaUrls)
    {
        if (string.IsNullOrWhiteSpace(evidenceMediaUrls))
        {
            return false;
        }

        var trimmed = evidenceMediaUrls.Trim();
        if (!trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return trimmed
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Any(x => !string.IsNullOrWhiteSpace(x));
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(element.GetString()))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    public static (bool IsValid, string? ErrorMessage) ValidateEvidenceRequirement(DiagnosisRecommendation? recommendation)
    {
        if (RequiresEvidenceForServiceRecommendation(recommendation) && !HasEvidenceMedia(recommendation?.EvidenceMediaUrls))
        {
            return (false, EvidenceRequiredMessage);
        }

        return (true, null);
    }

    public static string ResolveInitialStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? StatusActive
            : status;
    }

    public static void MarkSoftDeleted(DiagnosisRecommendation recommendation, DateTime deletedAt, long? deletedBy)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        recommendation.RecommendationStatus = StatusSoftDeletedByTeamLeader;
        recommendation.SoftDeletedAt = deletedAt;
        recommendation.SoftDeletedBy = deletedBy;
    }
}
