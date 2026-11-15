using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyGarage.Controllers;
using MyGarage.Interfaces;
using Xunit;

namespace MyGarage.Tests;

public class ControllerExceptionHandlingTests
{
    [Fact]
    public async Task InventoryAdminController_GetSettings_CorruptJson_ReturnsOkWithDefaultSettings()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "MyGarageSettingsTest_" + Guid.NewGuid().ToString("N"));
        var configDir = Path.Combine(tempRoot, "Configurations");
        Directory.CreateDirectory(configDir);
        var settingsFile = Path.Combine(configDir, "inventory-admin-settings.json");
        await File.WriteAllTextAsync(settingsFile, "{ invalid-json: [", Encoding.UTF8);

        try
        {
            var logger = new RecordingLogger<InventoryAdminController>();
            var controller = new InventoryAdminController(
                NoOpProxy<IInventoryAdminService>.Create(),
                NoOpProxy<MyGarage.Application.Authorization.IInventoryAuthorizationService>.Create(),
                new StubWebHostEnvironment(tempRoot),
                logger);

            var result = await controller.GetSettings();

            var ok = Assert.IsType<OkObjectResult>(result);
            var settings = Assert.IsType<InventoryAdminController.InventoryAdminSettings>(ok.Value);
            Assert.True(settings.DefaultWarehouseByItemType.ContainsKey("SELLABLE"));
            Assert.Equal(0, settings.DefaultWarehouseByItemType["SELLABLE"]);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task VehicleConditionLogController_UploadAttachment_PropagatesOperationCanceledException()
    {
        var logger = new RecordingLogger<VehicleConditionLogController>();
        var controller = CreateVehicleConditionLogController(
            new StubFileSecurityScanner((_, _) => throw new OperationCanceledException()),
            logger);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var file = new FormFile(stream, 0, stream.Length, "file", "photo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => controller.UploadAttachment(file));
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task VehicleConditionLogController_UploadAttachment_UnexpectedException_LogsErrorAndReturnsBadRequest()
    {
        var logger = new RecordingLogger<VehicleConditionLogController>();
        var controller = CreateVehicleConditionLogController(
            new StubFileSecurityScanner((_, _) => throw new InvalidOperationException("Scanner boom")),
            logger);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var file = new FormFile(stream, 0, stream.Length, "file", "photo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var result = await controller.UploadAttachment(file);

        var badRequest = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    private static VehicleConditionLogController CreateVehicleConditionLogController(
        IFileSecurityScanner scanner,
        ILogger<VehicleConditionLogController> logger)
    {
        var controller = new VehicleConditionLogController(
            NoOpProxy<IVehicleConditionLogCrudService>.Create(),
            NoOpProxy<IVehicleConditionLogReadService>.Create(),
            NoOpProxy<IVehicleConditionLogCommandFacade>.Create(),
            scanner,
            logger);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private sealed class StubFileSecurityScanner : IFileSecurityScanner
    {
        private readonly Func<FileSecurityScanRequest, CancellationToken, Task<FileSecurityScanResult>> _handler;

        public StubFileSecurityScanner(Func<FileSecurityScanRequest, CancellationToken, Task<FileSecurityScanResult>> handler)
        {
            _handler = handler;
        }

        public Task<FileSecurityScanResult> ScanAsync(FileSecurityScanRequest request, CancellationToken cancellationToken = default)
            => _handler(request, cancellationToken);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception, formatter(state, exception)));
        }
    }

    private class NoOpProxy<T> : DispatchProxy where T : class
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;

        public static T Create() => Create<T, NoOpProxy<T>>();
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public StubWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            WebRootPath = contentRootPath;
            ContentRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentRootPath);
            WebRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentRootPath);
        }

        public string WebRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
        public string ApplicationName { get; set; } = "MyGarage.Tests";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Testing";
    }
}
