using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class EmailDeliveryConfigurationTests
{
    [Fact]
    public async Task MissingSmtpConfigurationIsReportedAsFailure()
    {
        var service = new EmailService(
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<EmailService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendEmailAsync("test@example.com", "Test", "Test"));
    }
}
