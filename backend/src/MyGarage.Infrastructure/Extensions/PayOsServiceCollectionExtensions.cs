using PayOS;

namespace MyGarage.Infrastructure.Extensions;

internal static class PayOsServiceCollectionExtensions
{
    public static IServiceCollection AddPayOsIntegration(this IServiceCollection services)
    {
        services.AddKeyedSingleton("OrderClient", (sp, _) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var env = sp.GetRequiredService<IHostEnvironment>();
            return new PayOSClient(new PayOSOptions
            {
                ClientId = ResolvePayOsCredential(config, env, "PayOS:ClientId", "PAYOS_CLIENT_ID", "dev-local-order-client-id"),
                ApiKey = ResolvePayOsCredential(config, env, "PayOS:ApiKey", "PAYOS_API_KEY", "dev-local-order-api-key"),
                ChecksumKey = ResolvePayOsCredential(config, env, "PayOS:ChecksumKey", "PAYOS_CHECKSUM_KEY", "dev-local-order-checksum-key"),
                LogLevel = LogLevel.Debug,
            });
        });

        return services;
    }

    private static string ResolvePayOsCredential(
        IConfiguration config,
        IHostEnvironment env,
        string configurationKey,
        string environmentVariableName,
        string developmentPlaceholder)
    {
        var value = config[configurationKey] ?? Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        if (env.IsDevelopment())
        {
            return developmentPlaceholder;
        }

        throw new InvalidOperationException(
            $"PayOS: missing {configurationKey} (or env {environmentVariableName}). " +
            "Set values in appsettings, user secrets, or environment variables.");
    }
}
