using System.Text.RegularExpressions;

namespace MyGarage.Domain.Rules;

public static class CameraAccessRules
{
    public static string NormalizePlate(string value) => Regex.Replace(value.ToUpperInvariant(), "[^A-Z0-9]", "");
    public static bool CanRead(string role) => role is "ADMIN" or "MANAGER" or "RECEPTIONIST" or "GATE_SECURITY";
    public static bool CanManage(string role) => role is "ADMIN" or "MANAGER";
    public static bool CanConfirm(string role) => role is "ADMIN" or "MANAGER" or "GATE_SECURITY";
    public static bool PassUsable(string status,DateTime? passedAt,DateTime? expiresAtUtc,DateTime nowUtc) =>
        status is "APPROVED" or "ISSUED" && passedAt == null && (expiresAtUtc == null || expiresAtUtc > nowUtc);
    public static bool CanAccessBranch(string role, long? actorBranch, long targetBranch) =>
        CanRead(role) && (role == "ADMIN" || actorBranch == targetBranch);
    public static string? ExitBlock(bool vehicleMatched, bool passValid, bool ready, bool partsChecked) =>
        !vehicleMatched ? "Chưa xác định được xe và lệnh sửa chữa." :
        !passValid ? "Chưa có phiếu xuất cổng hợp lệ hoặc phiếu đã sử dụng." :
        !ready ? "Chưa hoàn tất thanh toán, QC hoặc công việc sửa chữa." :
        !partsChecked ? "Chưa xác nhận kiểm tra phụ tùng cũ." : null;
}
