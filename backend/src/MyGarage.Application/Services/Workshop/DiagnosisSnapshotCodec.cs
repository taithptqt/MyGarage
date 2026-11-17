using System;
using System.Text.Json;

namespace MyGarage.Services;

internal static class DiagnosisSnapshotCodec
{
        internal const string SnapshotStageCheckInT1 = "CHECK_IN_T1";
        internal const string SnapshotStageAfterDiagnosisV1 = "AFTER_DIAGNOSIS_V1";

        internal const string ApprovalStatePending = "PENDING";
        internal const string ApprovalStateApproved = "APPROVED";
        internal const string ApprovalStateRejected = "REJECTED";

        internal static readonly JsonSerializerOptions SnapshotReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        internal static readonly JsonSerializerOptions SnapshotWriteOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        internal sealed class DiagnosisIntakeSnapshotPayload
        {
            public string Stage { get; set; } = SnapshotStageCheckInT1;
            public long ServiceOrderId { get; set; }
            public string? DispatchTag { get; set; }
            public DateTime CheckInTime { get; set; }
            public int BufferMinutes { get; set; }
            public int MainServiceMinutes { get; set; }
            public int PackageEffectiveMinutes { get; set; }
            public DateTime T1EstimatedHandover { get; set; }
            public string IntakePackageCode { get; set; } = string.Empty;
            public string IntakePackageName { get; set; } = string.Empty;
            public bool RequiresCustomerConsent { get; set; }
            public bool CustomerConsentAccepted { get; set; }
            public string? CustomerConsentChannel { get; set; }
            public string? CustomerConsentNote { get; set; }
            public bool BypassConflictGuard { get; set; }
            public string ConflictLevel { get; set; } = "GREEN";
            public int ConflictMinutes { get; set; }
            public long? AssignedTeamId { get; set; }
            public long? LeadTechnicianId { get; set; }
            public long? AssistantTechnicianId { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        internal sealed class DiagnosisHandoverSnapshotPayload
        {
            public string Stage { get; set; } = SnapshotStageAfterDiagnosisV1;
            public long ServiceOrderId { get; set; }
            public DateTime T1EstimatedHandover { get; set; }
            public int AdditionalRepairMinutes { get; set; }
            public DateTime T2EstimatedHandover { get; set; }
            public string ApprovalState { get; set; } = ApprovalStatePending;
            public bool? CustomerApproved { get; set; }
            public string? DecisionChannel { get; set; }
            public string? DecisionNote { get; set; }
            public DateTime? CustomerDecisionAt { get; set; }
            public long? DecidedByStaffId { get; set; }
            public string? QuoteChannel { get; set; }
            public string? QuoteSummary { get; set; }
            public string? QuoteAttachmentUrl { get; set; }
            public DateTime CreatedAt { get; set; }
            public long? CreatedByStaffId { get; set; }
        }

        internal static bool TryParseIntakeSnapshot(string? snapshotData, out DiagnosisIntakeSnapshotPayload? payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(snapshotData))
            {
                return false;
            }

            try
            {
                payload = JsonSerializer.Deserialize<DiagnosisIntakeSnapshotPayload>(snapshotData, SnapshotReadOptions);
                return payload != null &&
                    string.Equals(payload.Stage, SnapshotStageCheckInT1, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                payload = null;
                return false;
            }
        }

        internal static bool TryParseHandoverSnapshot(string? snapshotData, out DiagnosisHandoverSnapshotPayload? payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(snapshotData))
            {
                return false;
            }

            try
            {
                payload = JsonSerializer.Deserialize<DiagnosisHandoverSnapshotPayload>(snapshotData, SnapshotReadOptions);
                return payload != null &&
                    string.Equals(payload.Stage, SnapshotStageAfterDiagnosisV1, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                payload = null;
                return false;
            }
        }

}
