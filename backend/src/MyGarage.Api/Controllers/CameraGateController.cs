using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.CameraAccess;
using MyGarage.Infrastructure.CameraAccess;
using System.Text;
using MyGarage.Middleware;

namespace MyGarage.Controllers;

public class CameraGateErrors : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var status=context.Exception switch { GateDeniedException=>403, KeyNotFoundException=>404,
            ArgumentException=>400, GateConflictException or DbUpdateConcurrencyException=>409, _=>0 };
        if(status==0) return;
        var message=status==403?"Bạn không có quyền xử lý cổng tại chi nhánh này.":
            status==404?"Không tìm thấy dữ liệu.":status==409?"Dữ liệu đã thay đổi hoặc chưa đủ điều kiện. "+context.Exception.Message:context.Exception.Message;
        context.Result=new ObjectResult(new {message}){StatusCode=status}; context.ExceptionHandled=true;
    }
}

[ApiController, Authorize(Roles="Staff"), CameraGateErrors, ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[Route("api/v1/gate")]
public class CameraGateController(CameraGateService service,CameraVideoService videos) : ControllerBase
{
    private GateActor Actor=>new(this.GetCurrentUserId()??0,this.GetCurrentRoleCode()??"",this.GetCurrentBranchId());
    [HttpGet("cameras")]
    public Task<object> Cameras([FromQuery]long? branchId)=>service.ListCameras(Actor,branchId);
    [HttpPost("cameras")]
    public Task<object> Create(CameraSave command)=>service.SaveCamera(Actor,null,command);
    [HttpPut("cameras/{id:long}")]
    public Task<object> Update(long id,CameraSave command)=>service.SaveCamera(Actor,id,command);
    [HttpPost("cameras/{id:long}/rotate-credential")]
    public Task<object> Rotate(long id)=>service.RotateCredential(Actor,id);
    [HttpPost("cameras/{id:long}/scan"), RequestSizeLimit(16_000_000)]
    public async Task<object> Scan(long id,IFormFile file)
    {
        if(file.Length<=0||file.Length>15_000_000) throw new ArgumentException("Ảnh vượt giới hạn 15 MB.");
        using var image=new MemoryStream(); await file.CopyToAsync(image);
        return await service.Scan(Actor,id,image.ToArray());
    }
    [HttpPost("cameras/{id:long}/videos"), RequestSizeLimit(250_000_000), RequestFormLimits(MultipartBodyLengthLimit=250_000_000)]
    public async Task<object> UploadVideo(long id,IFormFile file)
    {
        if(file.Length<=0||file.Length>240_000_000) throw new ArgumentException("Video tối đa 240 MB.");
        await using var stream=file.OpenReadStream(); return await videos.Open(Actor,id,stream);
    }
    [HttpPost("cameras/{id:long}/videos/sample")]
    public Task<object> SampleVideo(long id)=>videos.Open(Actor,id,null);
    [HttpPost("cameras/{id:long}/videos/{source}/analyze"), IdempotentCameraFrame, EnableRateLimiting("camera-tracking")]
    public Task<object> AnalyzeVideo(long id,string source,VideoFrame body)=>videos.Analyze(Actor,id,source,body.Time,body.Force,body.Mode,HttpContext.RequestAborted);
    [HttpPost("cameras/{id:long}/videos/{source}/close")]
    public async Task<IActionResult> CloseVideo(long id,string source){await videos.Close(Actor,id,source);return NoContent();}
    public record VideoFrame(double Time,bool Force=false,string Mode="balanced");
    [HttpGet("events")]
    public Task<object> Events([FromQuery]CameraFilter filter)=>service.List(Actor,filter);
    [HttpGet("events/{id:long}")]
    public Task<object> Detail(long id,[FromQuery]long? gatePassId)=>service.Detail(Actor,id,gatePassId);
    [HttpGet("events/{id:long}/evidence/{kind}")]
    public async Task<IActionResult> Evidence(long id,string kind)
    {
        var row=await service.Event(Actor,id);
        var image=kind=="snapshot"?row.Snapshot:kind=="crop"?row.PlateCrop:null;
        Response.Headers.CacheControl="private, no-store";
        Response.Headers["X-Content-Type-Options"]="nosniff";
        return image==null?NotFound():File(image,"image/jpeg");
    }
    [HttpPost("events/{id:long}/review")]
    public async Task<IActionResult> Review(long id,CameraReview command) {await service.Review(Actor,id,command);return NoContent();}
    [HttpGet("events/export")]
    public async Task<IActionResult> Export([FromQuery]CameraFilter filter)=>File(
        Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(await service.Export(Actor,filter))).ToArray(),"text/csv","gate-events.csv");
}

[ApiController, AllowAnonymous, CameraGateErrors, RequestSizeLimit(6_000_000), EnableRateLimiting("camera-ingestion"), ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[Route("api/v1/camera-ai/devices/{id:long}")]
public class CameraIngestionController(CameraGateService service) : ControllerBase
{
    private string Credential=>Request.Headers["X-Camera-Key"].ToString();
    [HttpGet("configuration")]
    public Task<object> Configuration(long id)=>service.Configuration(id,Credential);
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(long id,HeartbeatBody body) {await service.Heartbeat(id,Credential,body.Status);return NoContent();}
    [HttpPost("observations")]
    public Task<object> Observe(long id,CameraObservation body)=>service.Observe(id,Credential,body);
    public record HeartbeatBody(string Status);
}
