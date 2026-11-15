using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class LocalCameraAiFactAttribute : FactAttribute
{
    public LocalCameraAiFactAttribute()
    {
        if(Environment.GetEnvironmentVariable("MYGARAGE_CAMERA_E2E")!="1")
            Skip="Set MYGARAGE_CAMERA_E2E=1 with Camera AI on localhost:7861 and its local sample video available.";
    }
}

public class CameraVideoE2ETests
{
    [LocalCameraAiFact]
    public async Task UploadedLocalVideoUsesProtectedSession()
    {
        await using var factory=new TestWebApplicationFactory();
        await factory.SeedAsync(db=>{
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.CameraDevices.Add(new CameraDevice {Id=51,BranchId=1,Name="Upload simulation",Lane="SIMULATION",Direction="ENTRY"});
            return Task.CompletedTask;
        });
        using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",TestWebApplicationFactory.CreateJwtToken(200,"Staff","MANAGER",1));
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory!=null&&!File.Exists(Path.Combine(directory.FullName,"AGENTS.md"))) directory=directory.Parent;
        var videoPath=Environment.GetEnvironmentVariable("MYGARAGE_CAMERA_TEST_VIDEO")
            ??Path.Combine(directory!.FullName,"services","camera-ai","assets","traffic-sample.mp4");
        var scanTime=double.Parse(Environment.GetEnvironmentVariable("MYGARAGE_CAMERA_TEST_TIME")??"1",System.Globalization.CultureInfo.InvariantCulture);
        var expectedPlate=Environment.GetEnvironmentVariable("MYGARAGE_CAMERA_TEST_PLATE")??"65H02902";
        using var content=new MultipartFormDataContent();
        await using var file=File.OpenRead(videoPath);
        content.Add(new StreamContent(file),"file","traffic-sample.mp4");
        var opened=await client.PostAsync("/api/v1/gate/cameras/51/videos",content);
        Assert.True(opened.StatusCode==HttpStatusCode.OK,await opened.Content.ReadAsStringAsync());
        var source=(await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("source").GetString();
        try
        {
            using var other=factory.CreateClient();
            other.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",TestWebApplicationFactory.CreateJwtToken(201,"Staff","ADMIN",1));
            Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsJsonAsync($"/api/v1/gate/cameras/51/videos/{source}/analyze",new {time=1})).StatusCode);
            var analyzed=await client.PostAsJsonAsync($"/api/v1/gate/cameras/51/videos/{source}/analyze",new {time=scanTime,force=true});
            Assert.True(analyzed.StatusCode==HttpStatusCode.OK,await analyzed.Content.ReadAsStringAsync());
            var result=await analyzed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(result.GetProperty("plates").EnumerateArray(),x=>x.GetString()==expectedPlate);
            Assert.InRange(Math.Abs(result.GetProperty("time").GetDouble()-scanTime),0,.05);
            Assert.NotEmpty(Convert.FromBase64String(result.GetProperty("snapshot").GetString()!));
        }
        finally { await client.PostAsync($"/api/v1/gate/cameras/51/videos/{source}/close",null); }
    }

    [LocalCameraAiFact]
    public async Task LocalVideoThroughDotNetAiDatabaseEvidenceAndReview()
    {
        await using var factory=new TestWebApplicationFactory();
        await factory.SeedAsync(db=> {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.CameraDevices.Add(new CameraDevice {Id=50,BranchId=1,Name="Local video entrance",Lane="SIMULATION",Direction="ENTRY"});
            return Task.CompletedTask;
        });
        using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",TestWebApplicationFactory.CreateJwtToken(200,"Staff","MANAGER",1));
        var opened=await client.PostAsync("/api/v1/gate/cameras/50/videos/sample",null);
        Assert.Equal(HttpStatusCode.OK,opened.StatusCode);
        var source=(await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("source").GetString();
        try
        {
            foreach(var time in new[]{1.0,1.08})
            {
                var scan=await client.PostAsJsonAsync($"/api/v1/gate/cameras/50/videos/{source}/analyze",new {time});
                Assert.True(scan.StatusCode==HttpStatusCode.OK,await scan.Content.ReadAsStringAsync());
                var result=await scan.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Contains(result.GetProperty("plates").EnumerateArray(),x=>x.GetString()=="65H02902");
                Assert.Equal(source,result.GetProperty("source").GetString());
                Assert.True(result.GetProperty("width").GetInt32()>0);
                Assert.True(result.GetProperty("height").GetInt32()>0);
                var preview=Convert.FromBase64String(result.GetProperty("snapshot").GetString()!);
                Assert.Equal(new byte[]{0xff,0xd8},preview.Take(2).ToArray());
                var track=result.GetProperty("details").EnumerateArray().First(x=>x.GetProperty("text").GetString()=="65H02902");
                Assert.Equal(4,track.GetProperty("box").GetArrayLength());
                Assert.Equal(4,track.GetProperty("box_velocity").GetArrayLength());
                Assert.True(track.TryGetProperty("track_id",out _));
                Assert.False(track.TryGetProperty("crop_image",out _));
            }
            var repeated=await client.PostAsJsonAsync($"/api/v1/gate/cameras/50/videos/{source}/analyze",new {time=1.08});
            Assert.Equal(HttpStatusCode.OK,repeated.StatusCode);
            Assert.Equal(0,(await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results").GetArrayLength());
            using(var scope=factory.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<GarageContext>();
                Assert.Single(db.VehicleAccessEvents.Where(x=>x.Plate=="65H02902"));
            }
            var events=await client.GetFromJsonAsync<JsonElement>("/api/v1/gate/events");
            var row=events.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("plate").GetString()=="65H02902");
            var id=row.GetProperty("id").GetInt64();
            Assert.True(row.GetProperty("hasSnapshot").GetBoolean()); Assert.True(row.GetProperty("hasCrop").GetBoolean());
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/api/v1/gate/events/{id}/evidence/crop")).StatusCode);
            var confirmed=await client.PostAsJsonAsync($"/api/v1/gate/events/{id}/review",new {action="CONFIRM",reason="Local video simulation"});
            Assert.Equal(HttpStatusCode.NoContent,confirmed.StatusCode);
            var detail=await client.GetFromJsonAsync<JsonElement>($"/api/v1/gate/events/{id}");
            Assert.Contains(detail.GetProperty("audits").EnumerateArray(),x=>x.GetProperty("action").GetString()=="CONFIRM");
        }
        finally
        {
            await client.PostAsync($"/api/v1/gate/cameras/50/videos/{source}/close",null);
        }
    }
}
