using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.Exceptions;
using MyGarage.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace MyGarage.Controllers;

[ApiController]
[Authorize]
[Route("api/car-custom-requests")]
[Route("api/v1/car-custom-requests")]
public sealed class CarCustomRequestsController(IGarageContext db, IChatV2Service chat) : ControllerBase
{
    public const string MessageHeader = "[CUSTOM XE 3D]";
    private static readonly Dictionary<string, string> Paints = new()
    {
        ["#e4e6e7"] = "Trắng ngọc", ["#171c24"] = "Đen huyền", ["#a51d32"] = "Đỏ ruby",
        ["#164d78"] = "Xanh đại dương", ["#8f9aa8"] = "Bạc ánh kim", ["#b18a46"] = "Vàng đồng"
    };
    private static readonly Dictionary<string, string> Finishes = new() { ["gloss"] = "Bóng", ["matte"] = "Nhám satin", ["metallic"] = "Ánh kim" };
    private static readonly Dictionary<string, string> Rims = new()
    {
        ["stock"] = "Nguyên bản", ["graphite"] = "Đen graphite", ["silver"] = "Bạc sáng", ["bronze"] = "Đồng thể thao", ["white"] = "Trắng ceramic"
    };
    private static readonly Dictionary<string, string> Calipers = new() { ["stock"] = "Nguyên bản", ["red"] = "Đỏ racing", ["yellow"] = "Vàng sport", ["blue"] = "Xanh performance" };
    private static readonly Dictionary<string, string> Spoilers = new() { ["stock"] = "Không lắp", ["sport"] = "Sport đen (mô phỏng)", ["lip"] = "Lip thấp (mô phỏng)" };
    private static readonly Dictionary<string, string> Coatings = new() { ["stock"] = "Nguyên bản", ["black"] = "Đen bóng", ["carbon"] = "Vân carbon mô phỏng" };
    private static readonly Dictionary<string, string> Tints = new() { ["stock"] = "Nguyên bản", ["light"] = "Khói nhẹ", ["medium"] = "Khói đậm" };
    private static readonly Dictionary<string, string> Kits = new() { ["stock"] = "Nguyên bản", ["street"] = "Street: lip và ốp sườn", ["sport"] = "Sport: lip, ốp sườn và khuếch tán sau" };
    private static readonly Dictionary<string, string> Wheels = new() { ["stock"] = "Mâm theo xe", ["five"] = "5 chấu thể thao (mô phỏng)", ["split"] = "10 chấu thanh (mô phỏng)" };
    private static readonly Dictionary<string, string> Designs = new() { ["stripes"] = "Sọc racing", ["checker"] = "Cờ caro", ["lightning"] = "Tia chớp", ["number"] = "Số thi đấu", ["text"] = "Chữ cá nhân" };
    private static readonly Dictionary<string, string> Surfaces = new() { ["right"] = "Hông phải", ["left"] = "Hông trái", ["hood"] = "Nắp capo", ["roof"] = "Nóc xe" };
    private static readonly Dictionary<string, string> Accessories = new()
    {
        ["camera360"] = "Camera 360°", ["dashcam"] = "Camera hành trình", ["parkingSensors"] = "Cảm biến đỗ xe",
        ["audio"] = "Nâng cấp âm thanh", ["ambient"] = "Đèn viền nội thất", ["seatLeather"] = "Bọc ghế da", ["screen"] = "Màn hình giải trí"
    };
    private static bool ValidKey(Dictionary<string, string> values, string? key) => key != null && values.ContainsKey(key);

    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Submit([FromBody] CarCustomRequestDto request, CancellationToken cancellationToken)
    {
        var customerId = this.GetCurrentUserId();
        if (!customerId.HasValue) return Unauthorized();
        if (request.ClientMessageId == Guid.Empty ||
            (request.Paint != null && !Paints.ContainsKey(request.Paint)) ||
            request.Finish == null || !Finishes.ContainsKey(request.Finish) ||
            request.Rims == null || !Rims.ContainsKey(request.Rims) ||
            request.Calipers == null || !Calipers.ContainsKey(request.Calipers) ||
            request.Spoiler == null || !Spoilers.ContainsKey(request.Spoiler) ||
            !ValidKey(Coatings, request.Roof) || !ValidKey(Coatings, request.Mirrors) || !ValidKey(Tints, request.Tint) ||
            !ValidKey(Kits, request.BodyKit) || !ValidKey(Wheels, request.WheelStyle) || request.PaintMethod is not ("wrap" or "paint") ||
            request.Stickers == null || request.Stickers.Count > 6 || request.Stickers.Any(sticker => sticker == null || !ValidSticker(sticker)) ||
            request.Accessories == null || request.Accessories.Count > 7 || request.Accessories.Any(id => !ValidKey(Accessories, id)))
            return BadRequest(new { message = "Lựa chọn custom không hợp lệ." });
        if (request.Paint == null && request.Finish == "gloss" && request.Rims == "stock" && request.Calipers == "stock" && request.Spoiler == "stock" &&
            request.Roof == "stock" && request.Mirrors == "stock" && request.Tint == "stock" && request.BodyKit == "stock" && request.WheelStyle == "stock" && request.Stickers.Count == 0 && request.Accessories.Count == 0 && string.IsNullOrWhiteSpace(request.Notes))
            return BadRequest(new { message = "Chọn ít nhất một thay đổi trước khi gửi." });

        if (request.BranchId <= 0 || !await db.Branches.AnyAsync(branch => branch.Id == request.BranchId && branch.IsDeleted != true, cancellationToken))
            return BadRequest(new { message = "Chi nhánh tiếp nhận không hợp lệ." });
        if ((request.VehicleDescription?.Length ?? 0) > 160 || (request.Notes?.Length ?? 0) > 1000 || request.VehicleDescription?.Any(char.IsControl) == true)
            return BadRequest(new { message = "Thông tin xe hoặc ghi chú không hợp lệ." });
        var vehicleDescription = request.VehicleDescription?.Trim();
        if (request.VehicleId.HasValue)
        {
            var vehicle = await db.CustomerVehicles.Where(link => link.CustomerId == customerId.Value && link.VehicleId == request.VehicleId &&
                    link.IsCurrentOwner == true && link.IsDeleted != true && link.Vehicle.IsDeleted != true)
                .Select(link => new { link.Vehicle.LicensePlate, link.Vehicle.Model.ModelName }).FirstOrDefaultAsync(cancellationToken);
            if (vehicle == null) return BadRequest(new { message = "Xe không thuộc danh sách xe hiện tại của bạn." });
            vehicleDescription = $"{vehicle.ModelName} · {vehicle.LicensePlate}";
        }
        if (string.IsNullOrWhiteSpace(vehicleDescription)) return BadRequest(new { message = "Vui lòng cung cấp chiếc xe cần custom." });
        var lines = new List<string> { MessageHeader, $"Xe cần custom: {vehicleDescription}", "Model minh họa: VinFast Lux A2.0" };
        if (request.Paint != null) lines.Add($"Màu thân xe: {Paints[request.Paint]}");
        if (request.Paint != null || request.Finish != "gloss") lines.Add($"Phương án đổi màu: {(request.PaintMethod == "wrap" ? "Dán wrap" : "Sơn lại")}");
        if (request.Finish != "gloss") lines.Add($"Bề mặt sơn: {Finishes[request.Finish]}");
        if (request.Rims != "stock") lines.Add($"Màu mâm: {Rims[request.Rims]}");
        if (request.Calipers != "stock") lines.Add($"Màu cùm phanh: {Calipers[request.Calipers]}");
        if (request.Spoiler != "stock") lines.Add($"Cánh gió sau: {Spoilers[request.Spoiler]}");
        if (request.Roof != "stock") lines.Add($"Nóc xe: {Coatings[request.Roof!]}");
        if (request.Mirrors != "stock") lines.Add($"Ốp gương: {Coatings[request.Mirrors!]}");
        if (request.Tint != "stock") lines.Add($"Phim kính: {Tints[request.Tint!]}");
        if (request.BodyKit != "stock") lines.Add($"Gói ngoại thất: {Kits[request.BodyKit!]}");
        if (request.WheelStyle != "stock") lines.Add($"Kiểu mâm: {Wheels[request.WheelStyle!]}");
        foreach (var (sticker, index) in request.Stickers.Select((sticker, index) => (sticker, index)))
        {
            var text = sticker.Design is "text" or "number" ? $" | chữ: {sticker.Text}" : "";
            lines.Add(FormattableString.Invariant($"Decal {index + 1}: {Designs[sticker.Design!]} | {Surfaces[sticker.Surface!]} | màu {sticker.Color}{text} | rộng {sticker.Size:0.##} m (minh họa) | xoay {sticker.Rotation:0.##}° | dịch X/Y: {sticker.X:0.##}/{sticker.Y:0.##}"));
        }
        foreach (var accessory in request.Accessories.Distinct()) lines.Add($"Tiện nghi cần tư vấn: {Accessories[accessory]}");
        if (!string.IsNullOrWhiteSpace(request.Notes)) lines.Add($"Ghi chú khách hàng:\n{request.Notes.Trim()}");
        lines.Add("Yêu cầu tư vấn custom; cần kiểm tra vật tư và độ vừa thực tế trước khi thi công.");
        var content = string.Join('\n', lines);
        try
        {
            // Reuse durable customer messaging and its client-message deduplication.
            var conversation = await chat.CreateCustomerConversationAsync(customerId.Value,
                new ChatConversationCreateRequestDto { BranchId = request.BranchId, VehicleId = request.VehicleId }, cancellationToken);
            var message = await chat.SendCustomerMessageAsync(customerId.Value, conversation.Id,
                new ChatSendMessageRequestDto { ClientMessageId = request.ClientMessageId, MessageType = "TEXT", Content = content }, cancellationToken);
            if (message.Content != content) return Conflict(new { message = "Mã gửi đã dùng cho một cấu hình khác. Vui lòng gửi lại cấu hình mới." });
            return Ok(new { id = message.Id, conversationId = conversation.Id, createdAt = message.CreatedAt });
        }
        catch (ChatConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private static bool ValidSticker(CarCustomStickerDto sticker) => ValidKey(Designs, sticker.Design) && ValidKey(Surfaces, sticker.Surface) &&
        (sticker.Design is not ("text" or "number") || !string.IsNullOrWhiteSpace(sticker.Text)) &&
        sticker.Color != null && Regex.IsMatch(sticker.Color, "^#[0-9a-fA-F]{6}$") && (sticker.Text?.Length ?? 0) <= 20 && sticker.Text?.Any(char.IsControl) != true &&
        double.IsFinite(sticker.Size) && sticker.Size is >= 0.25 and <= 1.2 && double.IsFinite(sticker.Rotation) && sticker.Rotation is >= -180 and <= 180 &&
        double.IsFinite(sticker.X) && sticker.X is >= -1 and <= 1 && double.IsFinite(sticker.Y) && sticker.Y is >= -1 and <= 1;

    [HttpGet]
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> GetPage([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!this.IsStaffInAnyRole("ADMIN", "MANAGER")) return Forbid();
        var query = db.ChatMessages.AsNoTracking().Where(message => message.IsDeleted != true &&
            message.SenderType == "CUSTOMER" && message.Content.StartsWith(MessageHeader + "\n"));
        if (!this.IsStaffInAnyRole("ADMIN"))
        {
            var branchId = this.GetCurrentBranchId();
            if (!branchId.HasValue) return Forbid();
            query = query.Where(message => message.Conversation.BranchId == branchId.Value);
        }
        page = Math.Clamp(page, 1, 1000000); pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(message => message.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(message => new
            {
                message.Id, message.CreatedAt, message.Content,
                CustomerId = message.Conversation.CustomerId,
                CustomerName = message.Conversation.Customer.FullName,
                CustomerPhone = message.Conversation.Customer.Phone,
                BranchName = message.Conversation.Branch == null ? null : message.Conversation.Branch.Name
            }).ToListAsync(cancellationToken);
        return Ok(new { items, total, page, pageSize });
    }
}

public sealed class CarCustomRequestDto
{
    public long BranchId { get; set; }
    public long? VehicleId { get; set; }
    [StringLength(160)] public string? VehicleDescription { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public string? PaintMethod { get; set; } = "wrap";
    public string? Roof { get; set; } = "stock";
    public string? Mirrors { get; set; } = "stock";
    public string? Tint { get; set; } = "stock";
    public string? BodyKit { get; set; } = "stock";
    public string? WheelStyle { get; set; } = "stock";
    [MaxLength(6)] public List<CarCustomStickerDto>? Stickers { get; set; } = [];
    [MaxLength(7)] public List<string>? Accessories { get; set; } = [];
    public Guid ClientMessageId { get; set; }
    public string? Paint { get; set; }
    public string? Finish { get; set; }
    public string? Rims { get; set; }
    public string? Calipers { get; set; }
    public string? Spoiler { get; set; }
}

public sealed class CarCustomStickerDto
{
    public string? Design { get; set; }
    public string? Surface { get; set; }
    public string? Color { get; set; }
    [StringLength(20)] public string? Text { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Size { get; set; }
    public double Rotation { get; set; }
}
