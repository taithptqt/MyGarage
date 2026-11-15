using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using MyGarage.Application.CameraAccess;
using MyGarage.Data;
using MyGarage.Models;

namespace MyGarage.Infrastructure.CameraAccess;

public class CameraVideoService(GarageContext db,CameraGateService gate,IHttpClientFactory clients,IMemoryCache cache)
{
    private sealed record Session(long Actor,long Camera,SemaphoreSlim Lock)
    {
        public HashSet<string> RecordedPlates { get; } = new();
    }
    private static string Key(string source)=>"camera-video:"+source;
    private async Task<CameraDevice> Camera(GateActor actor,long id)
    {
        var camera=await db.CameraDevices.FindAsync(id)??throw new KeyNotFoundException();
        gate.Scope(actor,camera.BranchId);
        if(!camera.Enabled) throw new ArgumentException("Camera đang tắt.");
        return camera;
    }
    public async Task<object> Open(GateActor actor,long id,Stream? stream)
    {
        await Camera(actor,id);
        using var content=stream==null?null:new StreamContent(stream);
        using var response=await clients.CreateClient("camera-recognition").PostAsync(stream==null?"video/sample":"video/upload",content);
        if(!response.IsSuccessStatusCode) throw new GateConflictException("Không mở được video. Kiểm tra file và dịch vụ AI.");
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var source=doc.RootElement.GetProperty("source").GetString()!;
        cache.Set(Key(source),new Session(actor.Id,id,new SemaphoreSlim(1)),TimeSpan.FromMinutes(30));
        return new {source,duration=doc.RootElement.GetProperty("duration").GetDouble()};
    }
    private Session Owned(GateActor actor,long id,string source)
    {
        if(!cache.TryGetValue<Session>(Key(source),out var session)||session!.Actor!=actor.Id||session.Camera!=id) throw new GateDeniedException();
        return session;
    }
    public async Task<object> Analyze(GateActor actor,long id,string source,double time,bool force=false,string mode="balanced",CancellationToken cancellation=default)
    {
        var camera=await Camera(actor,id); var session=Owned(actor,id,source);
        if(!double.IsFinite(time)||time<0) throw new ArgumentException("Thời điểm video không hợp lệ.");
        if(mode is not ("fast" or "balanced" or "accurate" or "deep")) throw new ArgumentException("Chế độ nhận diện không hợp lệ.");
        await session.Lock.WaitAsync(cancellation);
        try
        {
            using var body=new StringContent(JsonSerializer.Serialize(new {source,time,mode,force,max_width=1280,sampling_fps=30,evidence=true}),Encoding.UTF8,"application/json");
            using var response=await clients.CreateClient("camera-recognition").PostAsync("video/analyze",body,cancellation);
            if(!response.IsSuccessStatusCode) throw new GateConflictException("Không quét được khung hình video.");
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation)); var root=doc.RootElement;
            var results=new List<object>();
            foreach(var item in root.GetProperty("details").EnumerateArray().Take(10))
            {
                cancellation.ThrowIfCancellationRequested();
                var plate=item.GetProperty("text").GetString(); if(string.IsNullOrWhiteSpace(plate)) plate="UNKNOWN";
                var normalized=MyGarage.Domain.Rules.CameraAccessRules.NormalizePlate(plate);
                if(session.RecordedPlates.Contains(normalized)) continue;
                // Local-video simulation records each distinct plate once per uploaded clip.
                var digest=SHA256.HashData(Encoding.UTF8.GetBytes($"{id}:{source}:{MyGarage.Domain.Rules.CameraAccessRules.NormalizePlate(plate)}"));
                var command=new CameraObservation(new Guid(digest.AsSpan(0,16)),$"LOCAL_VIDEO:{actor.Id}",plate,
                    Math.Min(item.GetProperty("ocr_confidence").GetDouble(),item.GetProperty("detection_confidence").GetDouble()),
                    DateTimeOffset.UtcNow,root.TryGetProperty("snapshot",out var snapshot)?snapshot.GetString():null,
                    item.TryGetProperty("crop_image",out var crop)?crop.GetString():null);
                results.Add(await gate.Ingest(camera,command));
                session.RecordedPlates.Add(normalized);
            }
            // Return geometry in the decoded frame's coordinate space, with its actual
            // timestamp. JPEG evidence remains private and is not sent on every UI repaint.
            var details=root.GetProperty("details").EnumerateArray().Select(row=>
                row.EnumerateObject().Where(p=>p.Name!="crop_image").ToDictionary(p=>p.Name,p=>p.Value.Clone())).ToList();
            return new {results,source,plates=root.GetProperty("plates").Clone(),
                time=root.GetProperty("time").GetDouble(),width=root.GetProperty("width").GetInt32(),
                height=root.GetProperty("height").GetInt32(),details,
                snapshot=root.TryGetProperty("snapshot",out var preview)?preview.GetString():null,
                ms=root.GetProperty("ms").GetDouble(),fps=root.GetProperty("fps").GetDouble()};
        }
        finally {session.Lock.Release();}
    }
    public async Task Close(GateActor actor,long id,string source)
    {
        await Camera(actor,id); Owned(actor,id,source); cache.Remove(Key(source));
        using var body=new StringContent(JsonSerializer.Serialize(new {source}),Encoding.UTF8,"application/json");
        using var response=await clients.CreateClient("camera-recognition").PostAsync("video/close",body);
    }
}
