using System.Text.Json;
using MyGarage.Application.CameraAccess;

namespace MyGarage.Infrastructure.CameraAccess;

public partial class CameraGateService
{
    public async Task<object> Scan(GateActor actor,long cameraId,byte[] image)
    {
        var camera=await db.CameraDevices.FindAsync(cameraId)??throw new KeyNotFoundException();
        Scope(actor,camera.BranchId);
        if(!camera.Enabled) throw new ArgumentException("Camera đang tắt.");
        using var content=new ByteArrayContent(image);
        using var response=await clients.CreateClient("camera-recognition").PostAsync("recognize?mode=balanced",content);
        if(!response.IsSuccessStatusCode) throw new GateConflictException("AI chưa xử lý được ảnh. Kiểm tra dịch vụ camera.");
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root=json.RootElement;
        var results=new List<object>();
        foreach(var item in root.GetProperty("details").EnumerateArray().Take(10))
        {
            var command=new CameraObservation(Guid.NewGuid(),$"MANUAL_IMAGE:{actor.Id}",item.GetProperty("text").GetString() is {Length:>0} text?text:"UNKNOWN",
                Math.Min(item.GetProperty("ocr_confidence").GetDouble(),item.GetProperty("detection_confidence").GetDouble()),
                DateTimeOffset.UtcNow,root.GetProperty("image").GetString(),
                item.TryGetProperty("crop_image",out var crop)?crop.GetString():null);
            results.Add(await Ingest(camera,command));
        }
        if(results.Count==0) throw new GateConflictException("Chưa tìm thấy biển số trong ảnh. Thử ảnh rõ hơn.");
        db.VehicleAccessAudits.Add(new() {BranchId=camera.BranchId,CameraId=cameraId,ActorId=actor.Id,Action="MANUAL_IMAGE_SCAN",At=DateTime.UtcNow});
        await db.SaveChangesAsync();
        return new {results};
    }
}
