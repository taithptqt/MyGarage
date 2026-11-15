using MyGarage.DTOs.Responses.Chat;
using MyGarage.Interfaces;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class ChatAttachmentUploadServiceTests
{
    private static readonly byte[] SamplePngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] SamplePdfBytes = "%PDF-1.7\n"u8.ToArray();

    [Theory]
    [InlineData(0, "Vui lòng chọn ít nhất 1 tệp đính kèm.")]
    [InlineData(-1, "Vui lòng chọn ít nhất 1 tệp đính kèm.")]
    [InlineData(ChatAttachmentUploadService.MaxAttachmentsPerRequest + 1, "Toi da 6 tep moi lan gui.")]
    public void ValidateAttachmentCount_ReturnsError_WhenOutOfRange(int count, string expectedMessage)
    {
        var sut = CreateSut(out _, out _);

        var error = sut.ValidateAttachmentCount(count);

        Assert.Equal(expectedMessage, error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(ChatAttachmentUploadService.MaxAttachmentsPerRequest)]
    public void ValidateAttachmentCount_ReturnsNull_WhenWithinLimit(int count)
    {
        var sut = CreateSut(out _, out _);

        var error = sut.ValidateAttachmentCount(count);

        Assert.Null(error);
    }

    [Fact]
    public async Task UploadAsync_ReturnsError_WhenFileListIsEmptyOrExceedsMaxCount()
    {
        var sut = CreateSut(out var scanner, out var storage);

        var emptyResult = await sut.UploadAsync([], "chat/customer");
        Assert.False(emptyResult.Succeeded);
        Assert.Equal("Vui lòng chọn ít nhất 1 tệp đính kèm.", emptyResult.ErrorMessage);
        Assert.Empty(emptyResult.Attachments);

        var oversizedBatch = Enumerable.Range(0, ChatAttachmentUploadService.MaxAttachmentsPerRequest + 1)
            .Select(i => CreateInput($"file-{i}.png", "image/png", SamplePngBytes))
            .ToList();

        var overLimitResult = await sut.UploadAsync(oversizedBatch, "chat/customer");
        Assert.False(overLimitResult.Succeeded);
        Assert.Equal("Toi da 6 tep moi lan gui.", overLimitResult.ErrorMessage);
        Assert.Empty(overLimitResult.Attachments);
        Assert.Empty(scanner.Requests);
        Assert.Empty(storage.Uploads);
    }

    [Theory]
    [InlineData("malware.exe", "application/octet-stream")]
    [InlineData("script.js", "application/javascript")]
    [InlineData("no-extension", "application/x-msdownload")]
    public async Task UploadAsync_RejectsUnsupportedExtensionOrMime_WithoutScanningOrUploading(
        string fileName,
        string contentType)
    {
        var sut = CreateSut(out var scanner, out var storage);
        var inputs = new[]
        {
            CreateInput(fileName, contentType, [0x01, 0x02, 0x03])
        };

        var result = await sut.UploadAsync(inputs, "chat/customer");

        Assert.False(result.Succeeded);
        Assert.Equal(
            $"Tệp thứ 1 không được hỗ trợ. Chỉ hỗ trợ {ChatAttachmentUploadService.SupportedAttachmentHint}.",
            result.ErrorMessage);
        Assert.Empty(result.Attachments);
        Assert.Empty(scanner.Requests);
        Assert.Empty(storage.Uploads);
    }

    [Fact]
    public async Task UploadAsync_RejectsNullOrEmptyOrOversizedDeclaredFile()
    {
        var sut = CreateSut(out var scanner, out var storage);

        var nullEntryResult = await sut.UploadAsync([null], "chat/customer");
        Assert.False(nullEntryResult.Succeeded);
        Assert.Equal("Tệp thứ 1 không hợp lệ.", nullEntryResult.ErrorMessage);

        var emptyLengthResult = await sut.UploadAsync(
            [new ChatAttachmentUploadInput("empty.png", "image/png", 0, () => new MemoryStream([]))],
            "chat/customer");
        Assert.False(emptyLengthResult.Succeeded);
        Assert.Equal("Tệp thứ 1 không hợp lệ.", emptyLengthResult.ErrorMessage);

        var declaredOversizeResult = await sut.UploadAsync(
            [
                new ChatAttachmentUploadInput(
                    "huge.pdf",
                    "application/pdf",
                    ChatAttachmentUploadService.MaxAttachmentSizeBytes + 1,
                    () => new MemoryStream(SamplePdfBytes))
            ],
            "chat/customer");
        Assert.False(declaredOversizeResult.Succeeded);
        Assert.Equal("Tệp thứ 1 vượt quá 10MB.", declaredOversizeResult.ErrorMessage);
        Assert.Empty(scanner.Requests);
        Assert.Empty(storage.Uploads);
    }

    [Fact]
    public async Task UploadAsync_RejectsOversizedStream_EvenWhenDeclaredLengthIsSmaller()
    {
        var sut = CreateSut(out var scanner, out var storage);
        var oversizedBytes = new byte[ChatAttachmentUploadService.MaxAttachmentSizeBytes + 1];
        var spoofedInput = new ChatAttachmentUploadInput(
            "spoofed.pdf",
            "application/pdf",
            128,
            () => new MemoryStream(oversizedBytes));

        var result = await sut.UploadAsync([spoofedInput], "chat/reception");

        Assert.False(result.Succeeded);
        Assert.Equal("Tệp thứ 1 vượt quá 10MB.", result.ErrorMessage);
        Assert.Empty(result.Attachments);
        Assert.Empty(scanner.Requests);
        Assert.Empty(storage.Uploads);
    }

    [Fact]
    public async Task UploadAsync_RejectsInfectedFile_WithExactError_AndPreventsPartialUploads()
    {
        var sut = CreateSut(out var scanner, out var storage);
        scanner.ResultsQueue.Enqueue(FileSecurityScanResult.Clean("magic-bytes"));
        scanner.ResultsQueue.Enqueue(FileSecurityScanResult.Infected("magic-bytes", "BAD_SIG", "Phát hiện nội dung độc hại."));

        var inputs = new[]
        {
            CreateInput("ok.png", "image/png", SamplePngBytes),
            CreateInput("bad.pdf", "application/pdf", SamplePdfBytes)
        };

        var result = await sut.UploadAsync(inputs, "chat/reception/1");

        Assert.False(result.Succeeded);
        Assert.Equal("Tệp thứ 2: Phát hiện nội dung độc hại.", result.ErrorMessage);
        Assert.Empty(result.Attachments);
        Assert.Equal(2, scanner.Requests.Count);
        Assert.Empty(storage.Uploads);
    }

    [Fact]
    public async Task UploadAsync_UsesFallbackSecurityMessage_WhenScannerMessageIsMissing()
    {
        var sut = CreateSut(out var scanner, out var storage);
        scanner.ResultsQueue.Enqueue(FileSecurityScanResult.Infected("clamav", "EICAR", null));

        var result = await sut.UploadAsync(
            [CreateInput("doc.pdf", "application/pdf", SamplePdfBytes)],
            "chat/customer");

        Assert.False(result.Succeeded);
        Assert.Equal("Tệp thứ 1: Tệp tải lên không an toàn.", result.ErrorMessage);
        Assert.Empty(storage.Uploads);
    }

    [Fact]
    public async Task UploadAsync_InfersExtensionAndContentType_AndUploadsSequentialAttachments()
    {
        var sut = CreateSut(out var scanner, out var storage);
        var inputs = new[]
        {
            CreateInput("clipboard-image", "image/png", SamplePngBytes),
            CreateInput("bao-cao.pdf", "application/octet-stream", SamplePdfBytes)
        };

        var result = await sut.UploadAsync(inputs, "/chat/reception/42/");

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(2, result.Attachments.Count);

        Assert.Equal(2, scanner.Requests.Count);
        Assert.Equal("attachment.png", scanner.Requests[0].FileName);
        Assert.Equal("image/png", scanner.Requests[0].ContentType);
        Assert.Equal(SamplePngBytes, scanner.Requests[0].FileBytes);

        Assert.Equal("bao-cao.pdf", scanner.Requests[1].FileName);
        Assert.Equal("application/pdf", scanner.Requests[1].ContentType);
        Assert.Equal(SamplePdfBytes, scanner.Requests[1].FileBytes);

        Assert.Equal(2, storage.Uploads.Count);
        Assert.Equal("chat/reception/42", storage.Uploads[0].FolderName);
        Assert.EndsWith(".png", storage.Uploads[0].FileName, StringComparison.Ordinal);
        Assert.Equal("image/png", storage.Uploads[0].ContentType);
        Assert.Equal(SamplePngBytes, storage.Uploads[0].Bytes);

        Assert.Equal("chat/reception/42", storage.Uploads[1].FolderName);
        Assert.EndsWith(".pdf", storage.Uploads[1].FileName, StringComparison.Ordinal);
        Assert.Equal("application/pdf", storage.Uploads[1].ContentType);
        Assert.Equal(SamplePdfBytes, storage.Uploads[1].Bytes);

        Assert.Equal(1, result.Attachments[0].SortOrder);
        Assert.Equal("image/png", result.Attachments[0].ContentType);
        Assert.Equal(SamplePngBytes.Length, result.Attachments[0].FileSize);
        Assert.StartsWith("https://cdn.example.test/chat/reception/42/", result.Attachments[0].AttachmentUrl, StringComparison.Ordinal);

        Assert.Equal(2, result.Attachments[1].SortOrder);
        Assert.Equal("application/pdf", result.Attachments[1].ContentType);
        Assert.Equal(SamplePdfBytes.Length, result.Attachments[1].FileSize);
        Assert.StartsWith("https://cdn.example.test/chat/reception/42/", result.Attachments[1].AttachmentUrl, StringComparison.Ordinal);
    }

    private static ChatAttachmentUploadService CreateSut(
        out FakeFileSecurityScanner scanner,
        out FakeCloudStorageService storage)
    {
        scanner = new FakeFileSecurityScanner();
        storage = new FakeCloudStorageService();
        return new ChatAttachmentUploadService(scanner, storage);
    }

    private static ChatAttachmentUploadInput CreateInput(string? fileName, string? contentType, byte[] bytes)
        => new(fileName, contentType, bytes.Length, () => new MemoryStream(bytes, writable: false));

    private sealed class FakeFileSecurityScanner : IFileSecurityScanner
    {
        public List<FileSecurityScanRequest> Requests { get; } = [];
        public Queue<FileSecurityScanResult> ResultsQueue { get; } = new();

        public Task<FileSecurityScanResult> ScanAsync(
            FileSecurityScanRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var result = ResultsQueue.Count > 0
                ? ResultsQueue.Dequeue()
                : FileSecurityScanResult.Clean("fake");
            return Task.FromResult(result);
        }
    }

    private sealed class FakeCloudStorageService : ICloudStorageService
    {
        public List<RecordedUpload> Uploads { get; } = [];

        public Task<string> UploadAsync(
            Stream stream,
            string folderName,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            Uploads.Add(new RecordedUpload(folderName, fileName, contentType, bytes));
            return Task.FromResult($"https://cdn.example.test/{folderName}/{fileName}");
        }

        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public sealed record RecordedUpload(string FolderName, string FileName, string ContentType, byte[] Bytes);
    }
}
