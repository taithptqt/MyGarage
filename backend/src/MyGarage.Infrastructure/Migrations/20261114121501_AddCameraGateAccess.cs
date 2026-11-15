using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGarage.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraGateAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE [Roles] DROP CONSTRAINT IF EXISTS [CK_Roles_RoleCode];
                ALTER TABLE [Roles] ADD CONSTRAINT [CK_Roles_RoleCode] CHECK
                ([RoleCode] IN ('ACCOUNTANT','QUALITY_CONTROL','FOREMAN','TEAM_LEADER','MANAGER','RECEPTIONIST','TECH','ADMIN','STORE_KEEPER','GATE_SECURITY'));
                IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [RoleCode]='GATE_SECURITY')
                BEGIN
                    IF COLUMNPROPERTY(OBJECT_ID('Roles'),'Id','IsIdentity')=1
                        INSERT INTO [Roles] ([RoleName],[RoleCode],[Scope],[IsActive],[IsDeleted],[CreatedAt],[UpdatedAt])
                        VALUES (N'Bảo vệ cổng','GATE_SECURITY','BRANCH',1,0,GETDATE(),GETDATE());
                    ELSE
                        INSERT INTO [Roles] ([Id],[RoleName],[RoleCode],[Scope],[IsActive],[IsDeleted],[CreatedAt],[UpdatedAt])
                        SELECT ISNULL(MAX([Id]),0)+1,N'Bảo vệ cổng','GATE_SECURITY','BRANCH',1,0,GETDATE(),GETDATE() FROM [Roles];
                END
                """);
            migrationBuilder.CreateTable(
                name: "CameraDevices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Lane = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EncryptedStreamUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CredentialHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    MinConfidence = table.Column<double>(type: "float", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: false),
                    LastHeartbeatAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Health = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CameraDevices_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleAccessAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: true),
                    CameraId = table.Column<long>(type: "bigint", nullable: true),
                    ActorId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Before = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    After = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleAccessAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleAccessEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    CameraId = table.Column<long>(type: "bigint", nullable: false),
                    EventKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerInstance = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RawPlate = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    VehicleId = table.Column<long>(type: "bigint", nullable: true),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    GatePassId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedBy = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Snapshot = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PlateCrop = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleAccessEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleAccessEvents_CameraDevices_CameraId",
                        column: x => x.CameraId,
                        principalTable: "CameraDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleAccessEvents_GatePass_GatePassId",
                        column: x => x.GatePassId,
                        principalTable: "GatePass",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleAccessEvents_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CameraDevices_BranchId",
                table: "CameraDevices",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAccessAudits_BranchId_At",
                table: "VehicleAccessAudits",
                columns: new[] { "BranchId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAccessEvents_BranchId_ReceivedAt",
                table: "VehicleAccessEvents",
                columns: new[] { "BranchId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAccessEvents_CameraId_EventKey",
                table: "VehicleAccessEvents",
                columns: new[] { "CameraId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAccessEvents_GatePassId",
                table: "VehicleAccessEvents",
                column: "GatePassId",
                unique: true,
                filter: "[GatePassId] IS NOT NULL AND [Status] = 'CONFIRMED'");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAccessEvents_VehicleId",
                table: "VehicleAccessEvents",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleAccessAudits");

            migrationBuilder.DropTable(
                name: "VehicleAccessEvents");

            migrationBuilder.DropTable(
                name: "CameraDevices");
        }
    }
}
