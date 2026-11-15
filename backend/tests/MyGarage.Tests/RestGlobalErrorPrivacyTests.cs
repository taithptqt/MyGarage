using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Application.Interfaces.Common;
using MyGarage.Middleware;
using Xunit;

namespace MyGarage.Tests;

public sealed class RestGlobalErrorPrivacyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_DoesNotExposeExceptionMessage(bool unauthorized)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance,
            new TestEnvironment(), new NoClassification());
        Exception exception = unauthorized ? new UnauthorizedAccessException("private-internal-value")
            : new InvalidOperationException("private-internal-value");
        Assert.True(await handler.TryHandleAsync(context, exception, default));
        Assert.Equal(unauthorized ? 401 : 400, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("private-internal-value", body);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
    }

    private sealed class NoClassification : IPersistenceExceptionClassifier
    {
        public PersistenceExceptionClassification Classify(Exception exception) => new(PersistenceExceptionKind.None);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
