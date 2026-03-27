// Mục đích: Tự động cập nhật trạng thái chiến dịch voucher theo lịch bắt đầu/kết thúc và đồng bộ ngân sách định kỳ.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class VoucherCampaignLifecycleService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VoucherCampaignLifecycleService> _logger;

        public VoucherCampaignLifecycleService(IServiceScopeFactory scopeFactory, ILogger<VoucherCampaignLifecycleService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var campaignService = scope.ServiceProvider.GetRequiredService<IVoucherCampaignService>();
                    var changed = await campaignService.SyncLifecycleStatusesAsync();

                    if (changed > 0)
                    {
                        _logger.LogInformation("Voucher campaign lifecycle sync updated {Count} campaign(s).", changed);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during voucher campaign lifecycle synchronization.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
    }
}
