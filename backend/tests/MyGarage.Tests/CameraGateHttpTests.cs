using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MyGarage.Application.CameraAccess;
using MyGarage.Infrastructure.CameraAccess;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class CameraGateHttpTests
{
    [Fact]
    public async Task CameraManagementEncryptsStreamsAndRestrictsWorkerSecrets()
    {
        await using var factory=new TestWebApplicationFactory(); await Seed(factory);
        using var reception=Client(factory,"RECEPTIONIST");
        var body=new CameraSave(1,"Denied camera","A","ENTRY","rtsp://camera.local/private");
        Assert.Equal(HttpStatusCode.Forbidden,(await reception.PostAsJsonAsync("/api/v1/gate/cameras",body)).StatusCode);
        using var manager=Client(factory,"MANAGER");
        var saved=await manager.PostAsJsonAsync("/api/v1/gate/cameras",body with {Name="Allowed camera"});
        Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        var created=await saved.Content.ReadFromJsonAsync<JsonElement>();
        var id=created.GetProperty("id").GetInt64(); var credential=created.GetProperty("credential").GetString();
        var list=await manager.GetStringAsync("/api/v1/gate/cameras");
        Assert.DoesNotContain("camera.local",list); Assert.DoesNotContain(credential!,list); Assert.DoesNotContain("credentialHash",list);
        await factory.SeedAsync(db=>{
            Assert.NotEqual(body.StreamUrl,db.CameraDevices.Single(x=>x.Id==id).EncryptedStreamUrl);
            return Task.CompletedTask;
        });
        using var device=factory.CreateClient(); device.DefaultRequestHeaders.Add("X-Camera-Key",credential);
        var configuration=await device.GetFromJsonAsync<JsonElement>($"/api/v1/camera-ai/devices/{id}/configuration");
        Assert.Equal(body.StreamUrl,configuration.GetProperty("streamUrl").GetString());
        var rotation=await manager.PostAsync($"/api/v1/gate/cameras/{id}/rotate-credential",null);
        Assert.True(rotation.StatusCode==HttpStatusCode.OK,await rotation.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden,(await device.GetAsync($"/api/v1/camera-ai/devices/{id}/configuration")).StatusCode);
    }

    [Fact]
    public async Task ClearedExitConsumesPassAndExpiredOrReusedPassIsBlocked()
    {
        await using var factory=new TestWebApplicationFactory(); await Seed(factory);
        await factory.SeedAsync(db=>{
            db.Vehicles.Add(new Vehicle {Id=60,LicensePlate="51H12345"});
            db.ServiceOrders.Add(new ServiceOrder {Id=60,Code="GATE-TEST",VehicleId=60,CustomerId=100,ExecuteBranchId=1,OriginBranchId=1,Status="READY",PaymentStatus="PAID",Qcstatus="PASS"});
            db.GatePasses.AddRange(new GatePass {Id=60,ServiceOrderId=60,QrCode="GP-TEST-60",ClearanceType="NORMAL",Status="ISSUED",IssuedBy=200},
                new GatePass {Id=61,ServiceOrderId=60,QrCode="GP-TEST-61",ClearanceType="NORMAL",Status="ISSUED",IssuedBy=200,ExpiresAtUtc=DateTime.UtcNow.AddMinutes(-1)});
            foreach(var id in new long[]{60,61,62}) db.VehicleAccessEvents.Add(new VehicleAccessEvent {Id=id,CameraId=10,BranchId=1,VehicleId=60,Direction="EXIT",Plate="51H12345"});
            return Task.CompletedTask;
        });
        using var security=Client(factory,"GATE_SECURITY");
        var released=await security.PostAsJsonAsync("/api/v1/gate/events/60/review",new CameraReview("CONFIRM",null,"Verified exit",GatePassId:60,OldPartsChecked:true));
        Assert.Equal(HttpStatusCode.NoContent,released.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await security.PostAsJsonAsync("/api/v1/gate/events/61/review",new CameraReview("CONFIRM",null,null,GatePassId:60))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await security.PostAsJsonAsync("/api/v1/gate/events/62/review",new CameraReview("CONFIRM",null,null,GatePassId:61,OldPartsChecked:true))).StatusCode);
        await factory.SeedAsync(db=>{
            Assert.Equal("SCANNED",db.GatePasses.Single(p=>p.Id==60).Status);
            Assert.NotNull(db.GatePasses.Single(p=>p.Id==60).PassedAt);
            Assert.Null(db.GatePasses.Single(p=>p.Id==61).PassedAt);
            return Task.CompletedTask;
        });
    }

    private static async Task Seed(TestWebApplicationFactory factory) => await factory.SeedAsync(db => {
        TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
        db.CameraDevices.AddRange(
            new CameraDevice {Id=10,BranchId=1,Name="Entry",Lane="A",Direction="ENTRY",CredentialHash=CameraGateService.Hash("device-key")},
            new CameraDevice {Id=20,BranchId=2,Name="Exit",Lane="B",Direction="EXIT",CredentialHash=CameraGateService.Hash("other-key")});
        db.VehicleAccessEvents.AddRange(
            new VehicleAccessEvent {Id=10,CameraId=10,BranchId=1,Plate="51H12345",RawPlate="51H12345",Snapshot=[255,216,255,217]},
            new VehicleAccessEvent {Id=20,CameraId=20,BranchId=2,Plate="65H02902",RawPlate="65H02902",Snapshot=[255,216,255,217]});
        return Task.CompletedTask;
    });
    private static HttpClient Client(TestWebApplicationFactory factory,string role)
    {
        var client=factory.CreateClient();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",TestWebApplicationFactory.CreateJwtToken(200,"Staff",role,1));
        return client;
    }

    [Fact]
    public async Task StaffListsImagesAndExportsRespectRoleAndBranch()
    {
        await using var factory=new TestWebApplicationFactory(); await Seed(factory);
        using var security=Client(factory,"GATE_SECURITY");
        var list=await security.GetFromJsonAsync<JsonElement>("/api/v1/gate/events");
        Assert.Equal(1,list.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK,(await security.GetAsync("/api/v1/gate/events/10/evidence/snapshot")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await security.GetAsync("/api/v1/gate/events/20/evidence/snapshot")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await security.GetAsync("/api/v1/gate/events?branchId=2")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await security.GetAsync("/api/v1/gate/events/export")).StatusCode);
        using var manager=Client(factory,"MANAGER");
        Assert.Equal(HttpStatusCode.OK,(await manager.GetAsync("/api/v1/gate/events/export")).StatusCode);
        using var tech=Client(factory,"TECH");
        Assert.Equal(HttpStatusCode.Forbidden,(await tech.GetAsync("/api/v1/gate/cameras")).StatusCode);
        using var anonymous=factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/api/v1/gate/events/10/evidence/snapshot")).StatusCode);
    }

    [Fact]
    public async Task IngestionAuthenticatesDeviceAndRetriesDoNotDuplicate()
    {
        await using var factory=new TestWebApplicationFactory(); await Seed(factory);
        using var client=factory.CreateClient();
        var body=new CameraObservation(Guid.NewGuid(),"test-worker","UNKNOWN",.4,DateTimeOffset.UtcNow,null,null);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/v1/camera-ai/devices/10/observations",body)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Camera-Key","device-key");
        var first=await client.PostAsJsonAsync("/api/v1/camera-ai/devices/10/observations",body);
        Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var second=await client.PostAsJsonAsync("/api/v1/camera-ai/devices/10/observations",body);
        Assert.True((await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("duplicate").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/v1/camera-ai/devices/20/observations",body)).StatusCode);
    }

    [Fact]
    public async Task ReceptionistCannotConfirmAndExitWithoutGatePassIsBlocked()
    {
        await using var factory=new TestWebApplicationFactory(); await Seed(factory);
        using var reception=Client(factory,"RECEPTIONIST");
        Assert.Equal(HttpStatusCode.Forbidden,(await reception.PostAsJsonAsync("/api/v1/gate/events/10/review",new CameraReview("CONFIRM",null,null))).StatusCode);
        using var security=Client(factory,"GATE_SECURITY");
        Assert.Equal(HttpStatusCode.OK,(await security.GetAsync("/api/v1/gate/events/10")).StatusCode);
        // Camera 20 is a different branch and must fail before evaluating gate eligibility.
        Assert.Equal(HttpStatusCode.Forbidden,(await security.PostAsJsonAsync("/api/v1/gate/events/20/review",new CameraReview("CONFIRM",null,null))).StatusCode);
        await factory.SeedAsync(db=> { db.VehicleAccessEvents.Add(new VehicleAccessEvent {Id=30,CameraId=10,BranchId=1,Direction="EXIT",Plate="51H12345"}); return Task.CompletedTask; });
        Assert.Equal(HttpStatusCode.Conflict,(await security.PostAsJsonAsync("/api/v1/gate/events/30/review",new CameraReview("CONFIRM",null,null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await security.PostAsJsonAsync("/api/v1/gate/events/30/review",new CameraReview("CONFIRM",null,"attempt",Override:true))).StatusCode);
        using var manager=Client(factory,"MANAGER");
        Assert.Equal(HttpStatusCode.NoContent,(await manager.PostAsJsonAsync("/api/v1/gate/events/30/review",new CameraReview("CONFIRM",null,"Approved recovery exception",Override:true))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await manager.PostAsJsonAsync("/api/v1/gate/events/30/review",new CameraReview("CONFIRM",null,"repeat",Override:true))).StatusCode);
    }
}
