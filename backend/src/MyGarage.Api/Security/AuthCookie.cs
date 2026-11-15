using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MyGarage.Security;

public static class AuthCookie
{
    public const string Name = "mygarage_access_token";

    public static void Append(
        HttpResponse response,
        string token,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var expireDays = double.TryParse(configuration["Jwt:ExpireDays"], out var days) && days > 0 ? days : 7d;
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() && !environment.IsEnvironment("Testing"),
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(expireDays),
            IsEssential = true
        });
    }

    public static void Delete(HttpResponse response, IHostEnvironment environment)
    {
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() && !environment.IsEnvironment("Testing"),
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }
}
