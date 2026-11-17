using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace MyGarage.Tests;

public sealed class AdminProductImageRouteTests
{
    [Fact]
    public async Task GalleryRoutes_ReturnSameResponseForMissingFile()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var paths = new[] { "/api/v1/admin/products/1/images", "/api/v1/admin/products/1/gallery-image", "/api/v1/admin/parts/1/gallery-image" };
        string? expectedBody = null;
        foreach (var path in paths)
        {
            using var form = new MultipartFormDataContent();
            using var response = await client.PostAsync(path, form);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            using var parsed = System.Text.Json.JsonDocument.Parse(body);
            Assert.Equal(400, parsed.RootElement.GetProperty("status").GetInt32());
            // The response may include a request-specific trace id, so compare the validation errors.
            var errors = parsed.RootElement.GetProperty("errors").GetRawText();
            expectedBody ??= errors;
            Assert.Equal(expectedBody, errors);
        }
    }
}