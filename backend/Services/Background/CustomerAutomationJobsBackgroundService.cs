using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyGarage.Interfaces;
using MyGarage.Options;

namespace MyGarage.Services;

public sealed class CustomerAutomationJobsBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<CustomerAutomationJobsOptions> _optionsMonitor;
    private readonly ILogger<CustomerAutomationJobsBackgroundService> _logger;
    private bool _disabledLogged;

    public CustomerAutomationJobsBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<CustomerAutomationJobsOptions> optionsMonitor,
        ILogger<CustomerAutomationJobsBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupDelay = TimeSpan.FromSeconds(Math.Max(0, _optionsMonitor.CurrentValue.StartupDelaySeconds));
        if (startupDelay > TimeSpan.Zero)
        {
            await Task.Delay(startupDelay, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var loopDelay = TimeSpan.FromSeconds(Math.Max(30, options.LoopIntervalSeconds));

            if (!options.Enabled)
            {
                if (!_disabledLogged)
                {
                    _logger.LogInformation("Customer automation jobs are disabled via configuration.");
                    _disabledLogged = true;
                }

                await Task.Delay(loopDelay, stoppingToken);
                continue;
            }

            _disabledLogged = false;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ICustomerAutomationJobProcessor>();

                var maintenanceResult = await RunWithRetryAsync(
                    "ProactiveMaintenanceOutreach",
                    processor.RunProactiveMaintenanceOutreachAsync,
                    options.Retry,
                    stoppingToken);
                LogResult(maintenanceResult);

                var surveyResult = await RunWithRetryAsync(
                    "PostServiceSurvey",
                    processor.RunPostServiceSurveyAsync,
                    options.Retry,
                    stoppingToken);
                LogResult(surveyResult);

                var insuranceResult = await RunWithRetryAsync(
                    "InsuranceExpiryReminder",
                    processor.RunInsuranceExpiryReminderAsync,
                    options.Retry,
                    stoppingToken);
                LogResult(insuranceResult);

                var cleanupResult = await RunWithRetryAsync(
                    "ChatbotSessionCleanup",
                    processor.RunChatbotSessionCleanupAsync,
                    options.Retry,
                    stoppingToken);
                LogResult(cleanupResult);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Customer automation jobs cycle failed.");
            }

            await Task.Delay(loopDelay, stoppingToken);
        }
    }

    private async Task<BackgroundJobRunResult> RunWithRetryAsync(
        string jobName,
        Func<CancellationToken, Task<BackgroundJobRunResult>> run,
        BackgroundJobRetryOptions retryOptions,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, retryOptions.MaxAttempts);
        var baseDelaySeconds = Math.Max(1, retryOptions.BaseDelaySeconds);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await run(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var isFinalAttempt = attempt >= maxAttempts;
                if (isFinalAttempt)
                {
                    _logger.LogError(
                        ex,
                        "Background job {JobName} failed after {AttemptCount} attempt(s).",
                        jobName,
                        attempt);

                    return new BackgroundJobRunResult
                    {
                        JobName = jobName,
                        Succeeded = false,
                        ErrorMessage = ex.Message
                    };
                }

                var backoffMultiplier = (int)Math.Pow(2, attempt - 1);
                var delaySeconds = Math.Min(300, baseDelaySeconds * backoffMultiplier);
                _logger.LogWarning(
                    ex,
                    "Background job {JobName} failed on attempt {Attempt}/{MaxAttempts}. Retrying in {DelaySeconds}s.",
                    jobName,
                    attempt,
                    maxAttempts,
                    delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }

        return new BackgroundJobRunResult
        {
            JobName = jobName,
            Succeeded = false,
            ErrorMessage = "Unexpected retry flow termination."
        };
    }

    private void LogResult(BackgroundJobRunResult result)
    {
        if (result.IsDisabled)
        {
            _logger.LogDebug("Background job {JobName} is disabled.", result.JobName);
            return;
        }

        if (!result.Succeeded)
        {
            _logger.LogError(
                "Background job {JobName} failed. Error: {ErrorMessage}",
                result.JobName,
                result.ErrorMessage ?? "Unknown");
            return;
        }

        _logger.LogInformation(
            "Background job {JobName} completed. Examined={ExaminedCount}, Notified={NotifiedCount}, Updated={UpdatedCount}, Skipped={SkippedCount}.",
            result.JobName,
            result.ExaminedCount,
            result.NotifiedCount,
            result.UpdatedCount,
            result.SkippedCount);
    }
}
