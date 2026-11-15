using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.Infrastructure.CameraAccess;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class CameraGateTests
{
    [Theory]
    [InlineData("51H-123.45", "51H12345")]
    [InlineData(" 65h 02902 ", "65H02902")]
    public void PlateNormalizationPreservesIdentity(string raw,string expected) => Assert.Equal(expected,CameraAccessRules.NormalizePlate(raw));

    [Theory]
    [InlineData("GATE_SECURITY",1,1,true)]
    [InlineData("GATE_SECURITY",1,2,false)]
    [InlineData("MANAGER",1,2,false)]
    [InlineData("ADMIN",1,2,true)]
    [InlineData("RECEPTIONIST",1,1,true)]
    [InlineData("TECH",1,1,false)]
    [InlineData("CUSTOMER",1,1,false)]
    public void BranchAuthorization(string role,long own,long target,bool allowed) =>
        Assert.Equal(allowed,CameraAccessRules.CanAccessBranch(role,own,target));

    [Fact]
    public void ReceptionistCanReadButCannotConfirmOrManage()
    {
        Assert.True(CameraAccessRules.CanRead("RECEPTIONIST"));
        Assert.False(CameraAccessRules.CanConfirm("RECEPTIONIST"));
        Assert.False(CameraAccessRules.CanManage("RECEPTIONIST"));
    }

    [Theory]
    [InlineData(false,true,true,true)]
    [InlineData(true,false,true,true)]
    [InlineData(true,true,false,true)]
    [InlineData(true,true,true,false)]
    public void ExitRequiresAllClearances(bool match,bool pass,bool readiness,bool parts) =>
        Assert.NotNull(CameraAccessRules.ExitBlock(match,pass,readiness,parts));

    [Fact]
    public void ExitAllowsOnlyCompleteClearance() => Assert.Null(CameraAccessRules.ExitBlock(true,true,true,true));

    [Fact]
    public void ExpiredCanceledPendingAndConsumedPassesCannotReleaseVehicle()
    {
        var now=DateTime.UtcNow;
        Assert.False(CameraAccessRules.PassUsable("ISSUED",null,now.AddMinutes(-1),now));
        Assert.False(CameraAccessRules.PassUsable("CANCELED",null,null,now));
        Assert.False(CameraAccessRules.PassUsable("PENDING",null,null,now));
        Assert.False(CameraAccessRules.PassUsable("SCANNED",now,null,now));
        Assert.True(CameraAccessRules.PassUsable("APPROVED",null,now.AddMinutes(1),now));
    }

    [Fact]
    public async Task EventDetailsAndEvidenceRejectOtherBranch()
    {
        await using var db=new GarageContext(new DbContextOptionsBuilder<GarageContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,new HttpContextAccessor());
        db.VehicleAccessEvents.Add(new VehicleAccessEvent {Id=1,CameraId=1,BranchId=2,Snapshot=[255,216,255,217]});
        await db.SaveChangesAsync();
        var service=new CameraGateService(db,new EphemeralDataProtectionProvider(),null!,null!,null!);
        await Assert.ThrowsAsync<GateDeniedException>(()=>service.Event(new GateActor(5,"GATE_SECURITY",1),1));
        Assert.Equal(2,(await service.Event(new GateActor(5,"GATE_SECURITY",2),1)).BranchId);
    }

    [Fact]
    public async Task DeviceCredentialIsScopedAndDisabledCameraRejected()
    {
        await using var db=new GarageContext(new DbContextOptionsBuilder<GarageContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,new HttpContextAccessor());
        db.CameraDevices.Add(new CameraDevice {Id=1,BranchId=1,CredentialHash=CameraGateService.Hash("secret")});
        await db.SaveChangesAsync();
        var service=new CameraGateService(db,new EphemeralDataProtectionProvider(),null!,null!,null!);
        await Assert.ThrowsAsync<GateDeniedException>(()=>service.Device(1,"wrong"));
        await Assert.ThrowsAsync<GateDeniedException>(()=>service.Device(2,"secret"));
        Assert.Equal(1,(await service.Device(1,"secret")).Id);
        db.CameraDevices.Local.Single().Enabled=false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<GateDeniedException>(()=>service.Device(1,"secret"));
    }
}
