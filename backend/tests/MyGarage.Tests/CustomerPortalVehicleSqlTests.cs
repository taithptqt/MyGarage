using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Application.CustomerPortal.DTOs;
using MyGarage.Data;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class CustomerPortalVehicleSqlTests
{
    [LocalSqlFact]
    public async Task CustomerRegistration_RespectsModelAndStaffForeignKeys()
    {
        var name = "MyGaragePortalVehicle_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new GarageContext(options, new HttpContextAccessor());
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE dbo.Staffs (Id bigint PRIMARY KEY);
                CREATE TABLE dbo.Customers (Id bigint PRIMARY KEY);
                CREATE TABLE dbo.VehicleBrands (Id bigint PRIMARY KEY, Name nvarchar(100) NOT NULL);
                CREATE TABLE dbo.VehicleModels (Id bigint PRIMARY KEY, BrandId bigint NOT NULL REFERENCES dbo.VehicleBrands(Id), ModelName nvarchar(100) NOT NULL, IsDeleted bit NULL);
                CREATE TABLE dbo.Vehicles (Id bigint IDENTITY PRIMARY KEY, LicensePlate varchar(50) NOT NULL, VIN varchar(50) NULL,
                    ModelId bigint NOT NULL REFERENCES dbo.VehicleModels(Id), Color nvarchar(50) NULL, ODO int NULL, OdoLastUpdatedAt datetime2 NULL,
                    ManufactureYear int NULL, CreatedAt datetime2 NULL, CreatedBy bigint NULL REFERENCES dbo.Staffs(Id),
                    UpdatedAt datetime2 NULL, UpdatedBy bigint NULL REFERENCES dbo.Staffs(Id), IsDeleted bit NULL,
                    DeletedAt datetime2 NULL, DeletedBy bigint NULL REFERENCES dbo.Staffs(Id));
                CREATE TABLE dbo.CustomerVehicles (Id bigint IDENTITY PRIMARY KEY, CustomerId bigint NOT NULL REFERENCES dbo.Customers(Id),
                    VehicleId bigint NOT NULL REFERENCES dbo.Vehicles(Id), IsCurrentOwner bit NULL, OwnershipStartDate date NULL, TransferDate date NULL,
                    Note nvarchar(500) NULL, CreatedAt datetime2 NULL, CreatedBy bigint NULL REFERENCES dbo.Staffs(Id),
                    UpdatedAt datetime2 NULL, UpdatedBy bigint NULL REFERENCES dbo.Staffs(Id), IsDeleted bit NULL,
                    DeletedAt datetime2 NULL, DeletedBy bigint NULL REFERENCES dbo.Staffs(Id));
                CREATE TABLE dbo.AuditLogs (Id bigint IDENTITY PRIMARY KEY, UserId bigint NOT NULL, UserType varchar(20) NOT NULL,
                    BranchId bigint NULL, Action varchar(50) NOT NULL, TableName varchar(100) NOT NULL, RecordId bigint NOT NULL,
                    OldValue nvarchar(max) NULL, NewValue nvarchar(max) NULL, IpAddress varchar(50) NULL, UserAgent nvarchar(500) NULL,
                    SessionId varchar(255) NULL, Timestamp datetime2 NULL);
                INSERT dbo.Customers VALUES (100);
                INSERT dbo.VehicleBrands VALUES (1, N'Toyota');
                INSERT dbo.VehicleModels VALUES (1, 1, N'Camry', 0);
                """);
            var actor = new UserActorContext(100, "Customer", "CUSTOMER", null, IsAdmin: false);
            var service = new CustomerPortalApplicationService(db, new CustomerPortalAuthorizationService(db));
            var command = new CustomerPortalVehicleUpsertCommand("51K-99999", 1, "ignored", "ignored", 2022, "White", null, 500);
            var created = await service.RegisterVehicleAsync(100, command, actor);
            var vehicle = await db.Vehicles.SingleAsync(v => v.Id == created.Id);
            var ownership = await db.CustomerVehicles.SingleAsync(v => v.VehicleId == created.Id);
            Assert.Equal(1, vehicle.ModelId);
            Assert.Null(vehicle.CreatedBy);
            Assert.Null(vehicle.UpdatedBy);
            Assert.Null(ownership.CreatedBy);
            Assert.Null(ownership.UpdatedBy);
            Assert.Equal("Camry", created.Model);
            var withVin = command with { LicensePlate = "51K-88888", Vin = "1HGCR2F83HA123456" };
            var second = await service.RegisterVehicleAsync(100, withVin, actor);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterVehicleAsync(
                100, withVin with { LicensePlate = "51K-77777", Vin = "1hgcr2f83ha123456" }, actor));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateVehicleAsync(
                100, created.Id, command with { LicensePlate = "51k-88888" }, actor));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateVehicleAsync(
                100, created.Id, command with { Vin = "1hgcr2f83ha123456" }, actor));
            Assert.NotEqual(created.Id, second.Id);
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
