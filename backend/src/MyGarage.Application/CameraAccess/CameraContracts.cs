using System.ComponentModel.DataAnnotations;

namespace MyGarage.Application.CameraAccess;

public record CameraSave(
    [Range(1, long.MaxValue)] long BranchId,
    [Required, StringLength(120)] string Name,
    [Required, StringLength(40)] string Lane,
    [RegularExpression("ENTRY|EXIT")] string Direction,
    [StringLength(2048)] string? StreamUrl,
    [Range(.1, 1)] double MinConfidence = .85,
    bool Enabled = true,
    [Range(1, 365)] int RetentionDays = 30);
public record CameraObservation(Guid EventKey, [Required, StringLength(80)] string WorkerInstance,
    [Required, StringLength(40)] string Plate, [Range(0, 1)] double Confidence,
    DateTimeOffset ObservedAt, string? SnapshotBase64, string? CropBase64);
public record CameraReview([Required, StringLength(30)] string Action,
    [StringLength(40)] string? Plate, [StringLength(1000)] string? Reason,
    long? GatePassId = null, bool Override = false, bool OldPartsChecked = false);
public record CameraFilter(long? BranchId = null, long? CameraId = null, string? Direction = null,
    string? Plate = null, string? Status = null, DateTime? From = null, DateTime? To = null,
    int Page = 1, int PageSize = 25);
