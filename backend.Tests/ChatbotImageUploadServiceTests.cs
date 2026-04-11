using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Data;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class ChatbotImageUploadServiceTests
{
    private const long CustomerId = 101;
    private const long SessionId = 5001;
    private const string SessionToken = "cbs_test_session_token";

    [Fact]
    public async Task UploadAndAnalyzeAsync_InvalidFile_ThrowsInvalidOperation()
    {
        await using var context = CreateContext(nameof(UploadAndAnalyzeAsync_InvalidFile_ThrowsInvalidOperation));
        SeedSession(context);

        var sut = CreateSut(context);
        var request = new UploadChatbotImageRequestDto
        {
            SessionId = SessionId,
            File = null
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UploadAndAnalyzeAsync(CustomerId, request));

        Assert.Contains("File is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAndAnalyzeAsync_UnsupportedType_ThrowsInvalidOperation()
    {
        await using var context = CreateContext(nameof(UploadAndAnalyzeAsync_UnsupportedType_ThrowsInvalidOperation));
        SeedSession(context);

        var sut = CreateSut(context);
        var request = new UploadChatbotImageRequestDto
        {
            SessionId = SessionId,
            File = CreateFormFile([0x41, 0x42, 0x43], "note.txt", "text/plain")
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UploadAndAnalyzeAsync(CustomerId, request));

        Assert.Contains("Unsupported image type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAndAnalyzeAsync_OversizedUpload_ThrowsInvalidOperation()
    {
        await using var context = CreateContext(nameof(UploadAndAnalyzeAsync_OversizedUpload_ThrowsInvalidOperation));
        SeedSession(context);

        var oversizedBytes = CreatePngBytes((8 * 1024 * 1024) + 32);
        var sut = CreateSut(context);
        var request = new UploadChatbotImageRequestDto
        {
            SessionId = SessionId,
            File = CreateFormFile(oversizedBytes, "large.png", "image/png")
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UploadAndAnalyzeAsync(CustomerId, request));

        Assert.Contains("size limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAndAnalyzeAsync_FailedStorage_ThrowsInvalidOperation()
    {
        await using var context = CreateContext(nameof(UploadAndAnalyzeAsync_FailedStorage_ThrowsInvalidOperation));
        SeedSession(context);

        var storage = new FakeCloudStorageService((_, _, _, _, _) =>
            throw new InvalidOperationException("Storage unavailable."));

        var sut = CreateSut(context, storage: storage);
        var request = new UploadChatbotImageRequestDto
        {
            SessionId = SessionId,
            Note = "dashboard warning",
            File = CreateFormFile(CreatePngBytes(), "dashboard.png", "image/png")
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UploadAndAnalyzeAsync(CustomerId, request));

        Assert.Contains("Failed to store uploaded image", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAndAnalyzeAsync_FailedVisionAnalysis_ThrowsInvalidOperation()
    {
        await using var context = CreateContext(nameof(UploadAndAnalyzeAsync_FailedVisionAnalysis_ThrowsInvalidOperation));
        SeedSession(context);

        var storage = new FakeCloudStorageService((_, _, _, _, _) =>
            Task.FromResult("https://storage.example.com/chatbot/sessions/5001/test.png"));
        var vision = new FakeVisionAnalyzer((_, _) =>
            throw new InvalidOperationException("Vision provider unavailable."));

        var sut = CreateSut(context, storage: storage, visionAnalyzer: vision);
        var request = new UploadChatbotImageRequestDto
        {
            SessionId = SessionId,
            Note = "possible dent on bumper",
            File = CreateFormFile(CreatePngBytes(), "damage.png", "image/png")
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UploadAndAnalyzeAsync(CustomerId, request));

        Assert.Contains("Failed to analyze uploaded image", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ChatbotImageUploadService CreateSut(
        GarageContext context,
        ICloudStorageService? storage = null,
        IVisionAnalyzer? visionAnalyzer = null,
        IFileSecurityScanner? scanner = null,
        DateTime? utcNow = null)
    {
        return new ChatbotImageUploadService(
            context,
            storage ?? new FakeCloudStorageService((_, _, _, _, _) => Task.FromResult("https://storage.example.com/test.png")),
            visionAnalyzer ?? new FakeVisionAnalyzer((_, _) => Task.FromResult(new VisionAnalyzerResult
            {
                ImageSummary = "Detected dashboard warning indicator.",
                SuspectedCategory = "DASHBOARD_WARNING",
                SuggestedServiceCategories = ["Diagnostics"]
            })),
            scanner ?? new FakeSecurityScanner(),
            new FakeClock(utcNow ?? new DateTime(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc)),
            NullLogger<ChatbotImageUploadService>.Instance);
    }

    private static GarageContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new GarageContext(options, new HttpContextAccessor());
    }

    private static void SeedSession(GarageContext context)
    {
        var now = new DateTime(2026, 4, 8, 9, 0, 0, DateTimeKind.Utc);
        context.Customers.Add(new Customer
        {
            Id = CustomerId,
            FullName = "Customer Upload",
            Phone = "0900000000",
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
        context.ChatbotSessions.Add(new ChatbotSession
        {
            Id = SessionId,
            SessionToken = SessionToken,
            CustomerId = CustomerId,
            Status = "ACTIVE",
            LastActivityAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });
        context.SaveChanges();
    }

    private static FormFile CreateFormFile(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static byte[] CreatePngBytes(int length = 64)
    {
        var targetLength = Math.Max(length, 8);
        var data = new byte[targetLength];
        data[0] = 0x89;
        data[1] = 0x50;
        data[2] = 0x4E;
        data[3] = 0x47;
        data[4] = 0x0D;
        data[5] = 0x0A;
        data[6] = 0x1A;
        data[7] = 0x0A;
        return data;
    }

    private sealed class FakeCloudStorageService : ICloudStorageService
    {
        private readonly Func<Stream, string, string, string, CancellationToken, Task<string>> _upload;

        public FakeCloudStorageService(Func<Stream, string, string, string, CancellationToken, Task<string>> upload)
        {
            _upload = upload;
        }

        public Task<string> UploadAsync(
            Stream stream,
            string folderName,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            return _upload(stream, folderName, fileName, contentType, cancellationToken);
        }
    }

    private sealed class FakeVisionAnalyzer : IVisionAnalyzer
    {
        private readonly Func<VisionAnalyzerRequest, CancellationToken, Task<VisionAnalyzerResult>> _analyze;

        public FakeVisionAnalyzer(Func<VisionAnalyzerRequest, CancellationToken, Task<VisionAnalyzerResult>> analyze)
        {
            _analyze = analyze;
        }

        public Task<VisionAnalyzerResult> AnalyzeAsync(
            VisionAnalyzerRequest request,
            CancellationToken cancellationToken = default)
        {
            return _analyze(request, cancellationToken);
        }
    }

    private sealed class FakeSecurityScanner : IFileSecurityScanner
    {
        public Task<FileSecurityScanResult> ScanAsync(
            FileSecurityScanRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(FileSecurityScanResult.Clean("TEST"));
        }
    }

    private sealed class FakeClock : ISystemClock
    {
        private readonly DateTime _utcNow;

        public FakeClock(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTime UtcNow => _utcNow;

        public DateTime LocalNow => _utcNow;

        public DateOnly LocalToday => DateOnly.FromDateTime(_utcNow);
    }
}
