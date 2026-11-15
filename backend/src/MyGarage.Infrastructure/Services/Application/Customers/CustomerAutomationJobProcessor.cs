using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyGarage.Interfaces;
using MyGarage.Options;

namespace MyGarage.Services;

public sealed class CustomerAutomationJobProcessor : ICustomerAutomationJobProcessor
{
    private readonly IOptions<CustomerAutomationJobsOptions> _options;
    private readonly ProactiveMaintenanceOutreachJobStep _maintenanceStep;
    private readonly PostServiceSurveyJobStep _surveyStep;
    private readonly InsuranceExpiryReminderJobStep _insuranceStep;
    private readonly ChatbotSessionCleanupJobStep _chatbotCleanupStep;

    public CustomerAutomationJobProcessor(
        IGarageContext context,
        IOptions<CustomerAutomationJobsOptions> options,
        ISystemClock clock,
        ILogger<CustomerAutomationJobProcessor> logger)
    {
        _options = options;
        var transactionRunner = new CustomerAutomationTransactionRunner(context, logger);
        _maintenanceStep = new ProactiveMaintenanceOutreachJobStep(context, clock, transactionRunner);
        _surveyStep = new PostServiceSurveyJobStep(context, clock, transactionRunner);
        _insuranceStep = new InsuranceExpiryReminderJobStep(context, clock, transactionRunner);
        _chatbotCleanupStep = new ChatbotSessionCleanupJobStep(context, clock, transactionRunner);
    }

    public Task<BackgroundJobRunResult> RunProactiveMaintenanceOutreachAsync(CancellationToken cancellationToken = default)
    {
        return _maintenanceStep.RunAsync(_options.Value.MaintenanceOutreach, cancellationToken);
    }

    public Task<BackgroundJobRunResult> RunPostServiceSurveyAsync(CancellationToken cancellationToken = default)
    {
        return _surveyStep.RunAsync(_options.Value.PostServiceSurvey, cancellationToken);
    }

    public Task<BackgroundJobRunResult> RunInsuranceExpiryReminderAsync(CancellationToken cancellationToken = default)
    {
        return _insuranceStep.RunAsync(_options.Value.InsuranceExpiryReminder, cancellationToken);
    }

    public Task<BackgroundJobRunResult> RunChatbotSessionCleanupAsync(CancellationToken cancellationToken = default)
    {
        return _chatbotCleanupStep.RunAsync(_options.Value.ChatbotSessionCleanup, cancellationToken);
    }
}
