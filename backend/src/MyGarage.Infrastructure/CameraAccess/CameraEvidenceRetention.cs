using Microsoft.EntityFrameworkCore;
using MyGarage.Data;

namespace MyGarage.Infrastructure.CameraAccess;

public class CameraEvidenceRetention(IServiceScopeFactory scopes, ILogger<CameraEvidenceRetention> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromHours(1));
        while(await timer.WaitForNextTickAsync(token))
        {
            try
            {
                using var scope=scopes.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<GarageContext>();
                var cameras=await db.CameraDevices.AsNoTracking().ToListAsync(token);
                foreach(var camera in cameras)
                {
                    var before=DateTime.UtcNow.AddDays(-camera.RetentionDays);
                    await db.VehicleAccessEvents.Where(x=>x.CameraId==camera.Id && x.ObservedAt<before && (x.Snapshot!=null || x.PlateCrop!=null))
                        .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Snapshot,(byte[]?)null).SetProperty(x=>x.PlateCrop,(byte[]?)null),token);
                }
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested){break;}
            catch(Exception error){logger.LogError(error,"Camera evidence retention failed");}
        }
    }
}
