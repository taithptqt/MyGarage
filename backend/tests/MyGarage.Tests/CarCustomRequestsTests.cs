using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Controllers;
using MyGarage.Data;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class CarCustomRequestsTests
{
    [Theory]
    [InlineData("MANAGER", "1", 1)]
    [InlineData("ADMIN", null, 2)]
    public async Task Inbox_RestrictsManagerToOwnBranch(string role, string? branch, int expected)
    {
        await using var db = Context();
        for (var i = 1; i <= 2; i++)
        {
            var customer = new Customer { Id = i, FullName = $"Customer {i}", Type = "Individual" };
            var conversation = new Conversation { Id = i, BranchId = i, Customer = customer };
            db.ChatMessages.Add(new ChatMessage { Id = i, Conversation = conversation, SenderId = i, SenderType = "CUSTOMER", Content = CarCustomRequestsController.MessageHeader + "\nSelections" });
        }
        await db.SaveChangesAsync();
        var controller = Controller(db, null!, "Staff", role, branch);
        var result = Assert.IsType<OkObjectResult>(await controller.GetPage());
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(expected, json.GetProperty("total").GetInt32());
        Assert.Equal(expected, json.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData("RECEPTIONIST", "1")]
    [InlineData("MANAGER", null)]
    public async Task Inbox_DeniesUnapprovedRoleOrMissingBranch(string role, string? branch)
    {
        await using var db = Context();
        Assert.IsType<ForbidResult>(await Controller(db, null!, "Staff", role, branch).GetPage());
    }

    [Fact]
    public async Task Submit_ValidatesChoicesBeforeSending()
    {
        await using var db = Context();
        var controller = Controller(db, null!, "Customer", "CUSTOMER", null);
        var request = Request();
        request.Rims = "unknown";
        Assert.IsType<BadRequestObjectResult>(await controller.Submit(request, default));
        request = Request(); request.Paint = null;
        Assert.IsType<BadRequestObjectResult>(await controller.Submit(request, default));
    }

    [Fact]
    public async Task Submit_SendsSelectionSnapshotWithCustomerAndRetryIdentifier()
    {
        await using var db = Context();
        db.Branches.Add(new Branch { Id = 1, Name = "Garage A", Status = "active" });
        await db.SaveChangesAsync();
        var chat = DispatchProxy.Create<IChatV2Service, ChatProxy>();
        var request = Request();
        request.Stickers = [new CarCustomStickerDto { Design = "text", Surface = "hood", Text = "CLUB 07", Color = "#ffffff", Size = 0.8, Rotation = 45, X = 0.25, Y = -0.5 }];
        request.BodyKit = "sport"; request.Notes = "Giữ logo nguyên bản";
        request.Accessories = ["camera360", "audio"];
        ChatSendMessageRequestDto? sent = null;
        ((ChatProxy)(object)chat).Handler = (method, args) =>
        {
            Assert.Equal(7L, args![0]);
            if (method.Name == nameof(IChatV2Service.CreateCustomerConversationAsync))
            {
                Assert.Equal(1L, ((ChatConversationCreateRequestDto)args[1]!).BranchId);
                return Task.FromResult(new ConversationDetailDto { Id = 42 });
            }
            Assert.Equal(nameof(IChatV2Service.SendCustomerMessageAsync), method.Name);
            Assert.Equal(42L, args[1]);
            sent = (ChatSendMessageRequestDto)args[2]!;
            return Task.FromResult(new ChatMessageDto { Id = 100, ConversationId = 42, Content = sent.Content! });
        };
        var controller = Controller(db, chat, "Customer", "CUSTOMER", null);
        Assert.IsType<OkObjectResult>(await controller.Submit(request, default));
        Assert.NotNull(sent);
        Assert.Equal(request.ClientMessageId, sent.ClientMessageId);
        Assert.Contains("Màu thân xe: Đỏ ruby", sent.Content);
        Assert.Contains("Xe cần custom: VinFast Lux A2.0 2021", sent.Content);
        Assert.DoesNotContain("Cánh gió sau", sent.Content);
        Assert.Contains("CLUB 07", sent.Content);
        Assert.Contains("0.8 m", sent.Content);
        Assert.Contains("45°", sent.Content);
        Assert.Contains("0.25/-0.5", sent.Content);
        Assert.Contains("Giữ logo nguyên bản", sent.Content);
        Assert.Contains("Camera 360°", sent.Content);
        Assert.Contains("Nâng cấp âm thanh", sent.Content);
        Assert.DoesNotContain("price", sent.Content!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("giá", sent.Content!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("design")]
    [InlineData("size")]
    [InlineData("rotation")]
    [InlineData("text")]
    [InlineData("count")]
    public async Task Submit_RejectsInvalidSticker(string invalid)
    {
        await using var db = Context();
        var request = Request();
        var sticker = new CarCustomStickerDto { Design = "text", Surface = "hood", Color = "#ffffff", Text = "MY GARAGE", Size = 0.7 };
        request.Stickers = [sticker];
        if (invalid == "design") sticker.Design = "unknown";
        if (invalid == "size") sticker.Size = 9;
        if (invalid == "rotation") sticker.Rotation = double.NaN;
        if (invalid == "text") sticker.Text = "text\nnot allowed";
        if (invalid == "count") request.Stickers = Enumerable.Repeat(sticker, 7).ToList();
        Assert.IsType<BadRequestObjectResult>(await Controller(db, null!, "Customer", "CUSTOMER", null).Submit(request, default));
    }

    [Fact]
    public async Task Submit_RejectsAnotherCustomersVehicle()
    {
        await using var db = Context();
        db.Branches.Add(new Branch { Id = 1, Name = "Garage A", Status = "active" });
        await db.SaveChangesAsync();
        var request = Request(); request.VehicleId = 999;
        Assert.IsType<BadRequestObjectResult>(await Controller(db, null!, "Customer", "CUSTOMER", null).Submit(request, default));
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData(null)]
    public async Task Submit_RejectsInactiveOrUnspecifiedBranch(string? status)
    {
        await using var db = Context();
        db.Branches.Add(new Branch { Id = 1, Name = "Garage A", Status = status });
        await db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await Controller(db, null!, "Customer", "CUSTOMER", null).Submit(Request(), default));
    }

    private static CarCustomRequestDto Request() => new() { BranchId = 1, VehicleDescription = "VinFast Lux A2.0 2021", ClientMessageId = Guid.NewGuid(), Paint = "#a51d32", Finish = "gloss", Rims = "stock", Calipers = "stock", Spoiler = "stock" };
    private static GarageContext Context() => new(new DbContextOptionsBuilder<GarageContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static CarCustomRequestsController Controller(GarageContext db, IChatV2Service chat, string role, string code, string? branch)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "7"), new(ClaimTypes.Role, role), new("role_code", code) };
        if (branch != null) claims.Add(new Claim("branch_id", branch));
        return new CarCustomRequestsController(db, chat) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } } };
    }

    public class ChatProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
}
