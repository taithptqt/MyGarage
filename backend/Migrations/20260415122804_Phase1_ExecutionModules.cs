using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGarage.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_ExecutionModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DISABLE TRIGGER ALL ON DATABASE;");
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_BookingGroupId",
                table: "Appointments");

            //migrationBuilder.DropForeignKey(
            //    name: "FK__Diagnoses__Appro__61BB7BD9",
            //    table: "Diagnoses");

            //migrationBuilder.DropForeignKey(
            //    name: "FK__Diagnoses__Techn__60C757A0",
            //    table: "Diagnoses");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingGroups_BranchId",
                table: "GroupSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingGroups_CreatedBy",
                table: "GroupSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingGroups_CustomerId",
                table: "GroupSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingGroups_UpdatedBy",
                table: "GroupSessions");

            //migrationBuilder.DropForeignKey(
            //    name: "FK__ServiceOr__Diagn__47C69FAC",
            //    table: "ServiceOrderDetails");

            //migrationBuilder.DropIndex(
            //    name: "IX_Diagnoses_ApprovedByForemanId",
            //    table: "Diagnoses");

            //migrationBuilder.DropIndex(
            //    name: "IX_Diagnoses_TechnicianId",
            //    table: "Diagnoses");

            //migrationBuilder.DropColumn(
            //    name: "VehicleClassId",
            //    table: "StandardServices");

            //migrationBuilder.DropColumn(
            //    name: "ApprovedByForemanId",
            //    table: "Diagnoses");

            //migrationBuilder.DropColumn(
            //    name: "TechnicianId",
            //    table: "Diagnoses");

            migrationBuilder.RenameColumn(
                name: "ValidUntil",
                table: "ServiceOrders",
                newName: "ArrivalTime");

            migrationBuilder.RenameColumn(
                name: "DiagnoseRecommendationId",
                table: "ServiceOrderDetails",
                newName: "QuotationDetailId");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceOrderDetails_DiagnoseRecommendationId",
                table: "ServiceOrderDetails",
                newName: "IX_ServiceOrderDetails_QuotationDetailId");

            migrationBuilder.RenameIndex(
                name: "UQ_BookingGroups_GroupCode",
                table: "GroupSessions",
                newName: "UQ_GroupSessions_GroupCode");

            migrationBuilder.RenameIndex(
                name: "IX_BookingGroups_Status_CreatedAt",
                table: "GroupSessions",
                newName: "IX_GroupSessions_Status_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_BookingGroups_CustomerId_CreatedAt",
                table: "GroupSessions",
                newName: "IX_GroupSessions_CustomerId_CreatedAt");

            migrationBuilder.RenameColumn(
                name: "DiagnoseId",
                table: "DiagnoseRecommendations",
                newName: "DiagnosisId");

            migrationBuilder.RenameIndex(
                name: "IX_DiagnoseRecommendations_DiagnoseId",
                table: "DiagnoseRecommendations",
                newName: "IX_DiagnoseRecommendations_DiagnosisId");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_BookingGroupId",
                table: "Appointments",
                newName: "IX_Appointments_GroupSessionId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WorkSlots",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "RESERVED",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldDefaultValue: "AVAILABLE");

            migrationBuilder.AlterColumn<long>(
                name: "BayId",
                table: "WorkSlots",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "AppointmentId",
                table: "WorkSlots",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdditional",
                table: "WorkSlots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SequenceOrder",
                table: "WorkSlots",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "ServiceOrderId",
                table: "WorkSlots",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Segment",
                table: "VehicleModels",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FuelType",
                table: "VehicleModels",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CylinderCapacity",
                table: "VehicleModels",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VehicleClassId",
                table: "VehicleModels",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EngineType",
                table: "StandardServices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                table: "Staffs",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Specialty",
                table: "Staffs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TeamId",
                table: "Staffs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofImageUrl",
                table: "ServiceStaffAssignments",
                type: "varchar(500)",
                unicode: false,
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresInsuranceProof",
                table: "ServiceStaffAssignments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "GroupSessionId",
                table: "ServiceOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceClaimMode",
                table: "ServiceOrders",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceDeductibleAmount",
                table: "ServiceOrders",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceDeductibleOption",
                table: "ServiceOrders",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceQuoteState",
                table: "ServiceOrders",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedPackageIds",
                table: "ServiceOrders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DiagnosisRecommendationId",
                table: "ServiceOrderDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "ServiceOrderDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectedAt",
                table: "QC_Inspections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverallScore",
                table: "QC_Inspections",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReInspectionCount",
                table: "QC_Inspections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TemplateId",
                table: "QC_Inspections",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConsumableThresholdAmount",
                table: "Products",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConsumable",
                table: "Products",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinStockLevel",
                table: "Products",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReorderPoint",
                table: "Products",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "ProductImages",
                type: "NVARCHAR(MAX)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldUnicode: false,
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentNote",
                table: "Diagnoses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DispatchTag",
                table: "Diagnoses",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEscalated",
                table: "Diagnoses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaDueAt",
                table: "Diagnoses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Diagnoses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DiagnosisAssignmentId",
                table: "DiagnoseRecommendations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendationStatus",
                table: "DiagnoseRecommendations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SoftDeletedAt",
                table: "DiagnoseRecommendations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SoftDeletedBy",
                table: "DiagnoseRecommendations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusChangedAt",
                table: "Bays",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservationStatus",
                table: "Bays",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SequenceOrder",
                table: "Bays",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceClaimMode",
                table: "Appointments",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "InsuranceCompanyId",
                table: "Appointments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceDeductibleAmount",
                table: "Appointments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceDeductibleOption",
                table: "Appointments",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceQuoteState",
                table: "Appointments",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedPackageIds",
                table: "Appointments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AI_FraudMonitors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    RiskLevel = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "FLAGGED"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AI_Fraud__3214EC0761F06F3E", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentInsuranceAssets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    AssetType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FileUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentInsuranceAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentInsuranceAssets_Appointments",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentInsuranceUploads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    BlobPath = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentInsuranceUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentInsuranceUploads_Appointments",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "attendance_adjustments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    staff_id = table.Column<long>(type: "bigint", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    adjustment_type = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    minutes = table.Column<int>(type: "int", nullable: true),
                    amount_impact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    approved_by = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_adjustments_Staffs_approved_by",
                        column: x => x.approved_by,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_attendance_adjustments_Staffs_staff_id",
                        column: x => x.staff_id,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BayAllocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BayId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    VehicleId = table.Column<long>(type: "bigint", nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AllocatedBy = table.Column<long>(type: "bigint", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedBy = table.Column<long>(type: "bigint", nullable: true),
                    PlannedDuration = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BayAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BayAllocations_Bays_BayId",
                        column: x => x.BayId,
                        principalTable: "Bays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BayAllocations_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BayAllocations_Staffs_AllocatedBy",
                        column: x => x.AllocatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BayAllocations_Staffs_ReleasedBy",
                        column: x => x.ReleasedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BayAllocations_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BayStatusLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BayId = table.Column<long>(type: "bigint", nullable: false),
                    OldStatus = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NewStatus = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedBy = table.Column<long>(type: "bigint", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BayStatusLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BayStatusLogs_Bays_BayId",
                        column: x => x.BayId,
                        principalTable: "Bays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BayStatusLogs_Staffs_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CapacityConfigs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    MaxBaysAvailable = table.Column<int>(type: "int", nullable: false),
                    BookingThresholdPercent = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    ActiveTechnicians = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    CreateBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    UpdateBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapacityConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapacityConfigs_Branch",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CapacityConfigs_CreateBy",
                        column: x => x.CreateBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CapacityConfigs_UpdateBy",
                        column: x => x.UpdateBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ConsumableReturnBundles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BundleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    TechnicianId = table.Column<long>(type: "bigint", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedBy = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumableReturnBundles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumableReturnBundles_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumableReturnBundles_Staffs_ReceivedBy",
                        column: x => x.ReceivedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumableReturnBundles_Staffs_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosisAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiagnosisId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedTeamId = table.Column<long>(type: "bigint", nullable: true),
                    TechnicianId = table.Column<long>(type: "bigint", nullable: true),
                    AssistantTechnicianId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedBayId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicianAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicianCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancellationRequestedBy = table.Column<long>(type: "bigint", nullable: true),
                    CancellationRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationDecisionBy = table.Column<long>(type: "bigint", nullable: true),
                    CancellationDecisionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationDecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupportRequestedBy = table.Column<long>(type: "bigint", nullable: true),
                    SupportRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupportRequestNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TeamLeaderApprovalStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    FinalApprovalStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ForemanRejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedByForemanId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedByForemanAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByTeamLeaderAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByTeamLeaderId = table.Column<long>(type: "bigint", nullable: true),
                    TeamLeaderNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ForemanNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequiresInsuranceProof = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ProofImageUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    EventType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    OldStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosisAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosisAssignments_Diagnoses",
                        column: x => x.DiagnosisId,
                        principalTable: "Diagnoses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_dependents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    staff_id = table.Column<long>(type: "bigint", nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    relationship = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    dob = table.Column<DateOnly>(type: "date", nullable: true),
                    tax_registration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_dependents", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_dependents_Staffs_staff_id",
                        column: x => x.staff_id,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "overtime_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    staff_id = table.Column<long>(type: "bigint", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    overtime_type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    hours = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    rate_multiplier = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    approved_by = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overtime_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_overtime_entries_Staffs_approved_by",
                        column: x => x.approved_by,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_overtime_entries_Staffs_staff_id",
                        column: x => x.staff_id,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StandardMinutes = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Packages_ServiceCategories",
                        column: x => x.CategoryId,
                        principalTable: "ServiceCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PartInventoryReservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkSlotId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    ReservedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "RESERVED"),
                    ExpiryTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    CreateBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    UpdateBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartInventoryReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartInventoryReservations_CreateBy",
                        column: x => x.CreateBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PartInventoryReservations_Product",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PartInventoryReservations_UpdateBy",
                        column: x => x.UpdateBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PartInventoryReservations_WorkSlot",
                        column: x => x.WorkSlotId,
                        principalTable: "WorkSlots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "payroll_periods",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    period_code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payroll_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_periods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pit_profiles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    staff_id = table.Column<long>(type: "bigint", nullable: false),
                    tax_code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    resident_type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    dependent_count = table.Column<int>(type: "int", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pit_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_pit_profiles_Staffs_staff_id",
                        column: x => x.staff_id,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QcChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ServiceCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QcChecklistTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceBOMs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    VehicleModelId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    CreateBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    UpdateBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceBOMs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceBOMs_CreateBy",
                        column: x => x.CreateBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceBOMs_Product",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceBOMs_Service",
                        column: x => x.ServiceId,
                        principalTable: "StandardServices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceBOMs_UpdateBy",
                        column: x => x.UpdateBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceBOMs_VehicleModel",
                        column: x => x.VehicleModelId,
                        principalTable: "VehicleModels",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrderInsuranceAssets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    AssetType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FileUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOrderInsuranceAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceOrderInsuranceAssets_ServiceOrders",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrderInsuranceUploads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    BlobPath = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOrderInsuranceUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceOrderInsuranceUploads_ServiceOrders",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "staff_contracts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    staff_id = table.Column<long>(type: "bigint", nullable: false),
                    contract_type = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    base_salary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    insurance_salary_base = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    allowance_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    pay_frequency = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    bank_account_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    bank_account_number = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    bank_name = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true),
                    tax_code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_contracts", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_contracts_Staffs_staff_id",
                        column: x => x.staff_id,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_salary_profiles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    staff_id = table.Column<long>(type: "bigint", nullable: false),
                    branch_id = table.Column<long>(type: "bigint", nullable: true),
                    base_salary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_salary_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_salary_profiles_Branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_staff_salary_profiles_Staffs_staff_id",
                        column: x => x.staff_id,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_salary_profiles_Staffs_updated_by",
                        column: x => x.updated_by,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    SpecialtyTag = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    LeaderId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "ACTIVE"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Teams_LeaderId",
                        column: x => x.LeaderId,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TechnicalProposalSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    DiagnosisId = table.Column<long>(type: "bigint", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LockedBy = table.Column<long>(type: "bigint", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalProposalSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalProposalSnapshots_Diagnoses_DiagnosisId",
                        column: x => x.DiagnosisId,
                        principalTable: "Diagnoses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicalProposalSnapshots_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimesheetEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckOutTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BreakMinutes = table.Column<int>(type: "int", nullable: false),
                    WorkType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimesheetEntries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimesheetEntries_Staffs_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimesheetEntries_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsumableReturnBundleItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BundleId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    MaterialReqDetailId = table.Column<long>(type: "bigint", nullable: true),
                    IssuedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UsedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumableReturnBundleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumableReturnBundleItems_ConsumableReturnBundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "ConsumableReturnBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConsumableReturnBundleItems_MaterialRequestDetails_MaterialReqDetailId",
                        column: x => x.MaterialReqDetailId,
                        principalTable: "MaterialRequestDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConsumableReturnBundleItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    StandardServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 1m),
                    SortOrder = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    IsDefault = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageItems_Packages",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageItems_StandardServices",
                        column: x => x.StandardServiceId,
                        principalTable: "StandardServices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "employee_loans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    staff_id = table.Column<long>(type: "bigint", nullable: false),
                    loan_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    outstanding_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    monthly_deduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    start_payroll_period_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_loans", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_loans_Staffs_staff_id",
                        column: x => x.staff_id,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_employee_loans_payroll_periods_start_payroll_period_id",
                        column: x => x.start_payroll_period_id,
                        principalTable: "payroll_periods",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payroll_runs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payroll_period_id = table.Column<long>(type: "bigint", nullable: false),
                    branch_id = table.Column<long>(type: "bigint", nullable: true),
                    run_no = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    calculated_by = table.Column<long>(type: "bigint", nullable: true),
                    calculated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approved_by = table.Column<long>(type: "bigint", nullable: true),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    paid_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_runs_Branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payroll_runs_Staffs_approved_by",
                        column: x => x.approved_by,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payroll_runs_Staffs_calculated_by",
                        column: x => x.calculated_by,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payroll_runs_payroll_periods_payroll_period_id",
                        column: x => x.payroll_period_id,
                        principalTable: "payroll_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "salary_advances",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    staff_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    request_date = table.Column<DateOnly>(type: "date", nullable: false),
                    approved_by = table.Column<long>(type: "bigint", nullable: true),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deduct_from_payroll_period_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_salary_advances", x => x.id);
                    table.ForeignKey(
                        name: "FK_salary_advances_Staffs_approved_by",
                        column: x => x.approved_by,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_salary_advances_Staffs_staff_id",
                        column: x => x.staff_id,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_salary_advances_payroll_periods_deduct_from_payroll_period_id",
                        column: x => x.deduct_from_payroll_period_id,
                        principalTable: "payroll_periods",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "QcChecklistItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QcInspectionId = table.Column<long>(type: "bigint", nullable: false),
                    TemplateItemId = table.Column<long>(type: "bigint", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Result = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    FailNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PhotoUrl = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedBy = table.Column<long>(type: "bigint", nullable: true),
                    QcChecklistTemplateId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QcChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QcChecklistItems_QC_Inspections_QcInspectionId",
                        column: x => x.QcInspectionId,
                        principalTable: "QC_Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QcChecklistItems_QcChecklistItems_TemplateItemId",
                        column: x => x.TemplateItemId,
                        principalTable: "QcChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QcChecklistItems_QcChecklistTemplates_QcChecklistTemplateId",
                        column: x => x.QcChecklistTemplateId,
                        principalTable: "QcChecklistTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QcChecklistItems_Staffs_CheckedBy",
                        column: x => x.CheckedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    DiagnosisId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    QuotationType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "NORMAL"),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    SubTotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalInsuranceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalCustomerAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    InsuranceApprovalEvidenceUrl = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "DRAFT"),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ServiceAdvisorId = table.Column<long>(type: "bigint", nullable: true),
                    SourceSnapshotId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicalApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    TechnicalApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TeamLeaderApprovalStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinalApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    FinalApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalApprovalStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerDecisionBy = table.Column<long>(type: "bigint", nullable: true),
                    CustomerDecisionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerDecisionStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CustomerNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InternalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsAppliedToServiceOrder = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AppliedBy = table.Column<long>(type: "bigint", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotations_AppliedBy",
                        column: x => x.AppliedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Quotations_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Quotations_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Quotations_Diagnoses",
                        column: x => x.DiagnosisId,
                        principalTable: "Diagnoses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Quotations_ServiceAdvisor",
                        column: x => x.ServiceAdvisorId,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Quotations_ServiceOrders",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Quotations_TechnicalProposalSnapshots_SourceSnapshotId",
                        column: x => x.SourceSnapshotId,
                        principalTable: "TechnicalProposalSnapshots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Quotations_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TechnicalProposalSnapshotDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    DiagnosisRecommendationId = table.Column<long>(type: "bigint", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    TechnicianNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalProposalSnapshotDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalProposalSnapshotDetails_TechnicalProposalSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "TechnicalProposalSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payroll_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payroll_run_id = table.Column<long>(type: "bigint", nullable: false),
                    staff_id = table.Column<long>(type: "bigint", nullable: false),
                    base_salary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    attendance_deduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    overtime_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    allowance_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    commission_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    bonus_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    penalty_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    advance_deduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    loan_deduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    insurance_employee_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    insurance_company_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    pit_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    other_deduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    net_pay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_items_Staffs_staff_id",
                        column: x => x.staff_id,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payroll_items_payroll_runs_payroll_run_id",
                        column: x => x.payroll_run_id,
                        principalTable: "payroll_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationAccessLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    SentToEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ClientNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationAccessLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationAccessLinks_Quotations",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    DiagnosisRecommendationId = table.Column<long>(type: "bigint", nullable: true),
                    ItemType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPriceAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPriceAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayerType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    InsuranceCompanyId = table.Column<long>(type: "bigint", nullable: true),
                    InsuranceRequestedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CustomerCoPayAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InsuranceCoveredAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CustomerCoveredAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsBlocking = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: true),
                    IsReturnOldPart = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationDetails_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuotationDetails_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuotationDetails_DiagnoseRecommendations",
                        column: x => x.DiagnosisRecommendationId,
                        principalTable: "DiagnoseRecommendations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuotationDetails_InsuranceCompanies",
                        column: x => x.InsuranceCompanyId,
                        principalTable: "InsuranceCompanies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuotationDetails_Quotations",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuotationDetails_ServiceOrders",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuotationDetails_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "payslips",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payroll_item_id = table.Column<long>(type: "bigint", nullable: false),
                    slip_no = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    issued_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    issued_by = table.Column<long>(type: "bigint", nullable: true),
                    file_url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    viewed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payslips", x => x.id);
                    table.ForeignKey(
                        name: "FK_payslips_Staffs_issued_by",
                        column: x => x.issued_by,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payslips_payroll_items_payroll_item_id",
                        column: x => x.payroll_item_id,
                        principalTable: "payroll_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MandatoryOverrideRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    QuotationDetailId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedBy = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedBy = table.Column<long>(type: "bigint", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MandatoryOverrideRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MandatoryOverrideRequests_QuotationDetails_QuotationDetailId",
                        column: x => x.QuotationDetailId,
                        principalTable: "QuotationDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MandatoryOverrideRequests_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MandatoryOverrideRequests_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSlots_AppointmentId",
                table: "WorkSlots",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSlots_ServiceOrderId",
                table: "WorkSlots",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_TeamId",
                table: "Staffs",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_GroupSessionId",
                table: "ServiceOrders",
                column: "GroupSessionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceOrders_OrderCategory",
                table: "ServiceOrders",
                sql: "[OrderCategory] IS NULL OR ([OrderCategory]='WARRANTLY' OR [OrderCategory]='REWORK' OR [OrderCategory]='INSURANCE_REPAIR' OR [OrderCategory]='QUICK_SERVICE' OR [OrderCategory]='GENERAL_REPAIR' OR [OrderCategory]='MAINTENANCE')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceOrders_Status",
                table: "ServiceOrders",
                sql: "[Status] IN ('NEW','CHECKED_IN','WAITING','DIAGNOSING','QUOTING','WAITING_APPROVAL','APPROVED','IN_PROGRESS','REPAIRING','READY','WAITING_PAYMENT','CLOSED','CANCELED')");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_DiagnosisRecommendationId",
                table: "ServiceOrderDetails",
                column: "DiagnosisRecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_QC_Inspections_TemplateId",
                table: "QC_Inspections",
                column: "TemplateId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GroupSessions_Status",
                table: "GroupSessions",
                sql: "[Status] IN ('PENDING','CONFIRMED','PARTIAL_CONFIRMED','CANCELED','COMPLETED')");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnoseRecommendations_DiagnosisAssignmentId",
                table: "DiagnoseRecommendations",
                column: "DiagnosisAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_InsuranceCompanyId",
                table: "Appointments",
                column: "InsuranceCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentInsuranceAssets_Appointment_Type_Order",
                table: "AppointmentInsuranceAssets",
                columns: new[] { "AppointmentId", "AssetType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentInsuranceUploads_AppointmentId",
                table: "AppointmentInsuranceUploads",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentInsuranceUploads_Status_CreatedAt",
                table: "AppointmentInsuranceUploads",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_adjustments_approved_by",
                table: "attendance_adjustments",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_adjustments_staff_id",
                table: "attendance_adjustments",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_BayAllocations_AllocatedBy",
                table: "BayAllocations",
                column: "AllocatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BayAllocations_BayId",
                table: "BayAllocations",
                column: "BayId");

            migrationBuilder.CreateIndex(
                name: "IX_BayAllocations_ReleasedBy",
                table: "BayAllocations",
                column: "ReleasedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BayAllocations_ServiceOrderId",
                table: "BayAllocations",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_BayAllocations_VehicleId",
                table: "BayAllocations",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_BayStatusLogs_BayId",
                table: "BayStatusLogs",
                column: "BayId");

            migrationBuilder.CreateIndex(
                name: "IX_BayStatusLogs_ChangedBy",
                table: "BayStatusLogs",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CapacityConfigs_BranchId",
                table: "CapacityConfigs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CapacityConfigs_CreateBy",
                table: "CapacityConfigs",
                column: "CreateBy");

            migrationBuilder.CreateIndex(
                name: "IX_CapacityConfigs_UpdateBy",
                table: "CapacityConfigs",
                column: "UpdateBy");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableReturnBundleItems_BundleId",
                table: "ConsumableReturnBundleItems",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableReturnBundleItems_MaterialReqDetailId",
                table: "ConsumableReturnBundleItems",
                column: "MaterialReqDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableReturnBundleItems_ProductId",
                table: "ConsumableReturnBundleItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableReturnBundles_ReceivedBy",
                table: "ConsumableReturnBundles",
                column: "ReceivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableReturnBundles_ServiceOrderId",
                table: "ConsumableReturnBundles",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableReturnBundles_TechnicianId",
                table: "ConsumableReturnBundles",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosisAssignments_DiagnosisId",
                table: "DiagnosisAssignments",
                column: "DiagnosisId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_dependents_staff_id",
                table: "employee_dependents",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_loans_staff_id",
                table: "employee_loans",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_loans_start_payroll_period_id",
                table: "employee_loans",
                column: "start_payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_MandatoryOverrideRequests_QuotationDetailId",
                table: "MandatoryOverrideRequests",
                column: "QuotationDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_MandatoryOverrideRequests_QuotationId",
                table: "MandatoryOverrideRequests",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_MandatoryOverrideRequests_ServiceOrderId",
                table: "MandatoryOverrideRequests",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_overtime_entries_approved_by",
                table: "overtime_entries",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_overtime_entries_staff_id",
                table: "overtime_entries",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_PackageItems_StandardServiceId",
                table: "PackageItems",
                column: "StandardServiceId");

            migrationBuilder.CreateIndex(
                name: "UQ_PackageItems_Package_Service",
                table: "PackageItems",
                columns: new[] { "PackageId", "StandardServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_CategoryId",
                table: "Packages",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "UQ_Packages_Code",
                table: "Packages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartInventoryReservations_CreateBy",
                table: "PartInventoryReservations",
                column: "CreateBy");

            migrationBuilder.CreateIndex(
                name: "IX_PartInventoryReservations_ProductId",
                table: "PartInventoryReservations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PartInventoryReservations_UpdateBy",
                table: "PartInventoryReservations",
                column: "UpdateBy");

            migrationBuilder.CreateIndex(
                name: "IX_PartInventoryReservations_WorkSlotId",
                table: "PartInventoryReservations",
                column: "WorkSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_items_payroll_run_id",
                table: "payroll_items",
                column: "payroll_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_items_staff_id",
                table: "payroll_items",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_runs_approved_by",
                table: "payroll_runs",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_runs_branch_id",
                table: "payroll_runs",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_runs_calculated_by",
                table: "payroll_runs",
                column: "calculated_by");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_runs_payroll_period_id",
                table: "payroll_runs",
                column: "payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_payslips_issued_by",
                table: "payslips",
                column: "issued_by");

            migrationBuilder.CreateIndex(
                name: "IX_payslips_payroll_item_id",
                table: "payslips",
                column: "payroll_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pit_profiles_staff_id",
                table: "pit_profiles",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_QcChecklistItems_CheckedBy",
                table: "QcChecklistItems",
                column: "CheckedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QcChecklistItems_QcChecklistTemplateId",
                table: "QcChecklistItems",
                column: "QcChecklistTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_QcChecklistItems_QcInspectionId",
                table: "QcChecklistItems",
                column: "QcInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_QcChecklistItems_TemplateItemId",
                table: "QcChecklistItems",
                column: "TemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationAccessLinks_ExpiresAt",
                table: "QuotationAccessLinks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationAccessLinks_QuotationId_Status",
                table: "QuotationAccessLinks",
                columns: new[] { "QuotationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_QuotationAccessLinks_TokenHash",
                table: "QuotationAccessLinks",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_ApprovedBy",
                table: "QuotationDetails",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_CreatedBy",
                table: "QuotationDetails",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_DiagnosisRecommendationId",
                table: "QuotationDetails",
                column: "DiagnosisRecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_InsuranceCompanyId",
                table: "QuotationDetails",
                column: "InsuranceCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_QuotationId_ApprovalStatus",
                table: "QuotationDetails",
                columns: new[] { "QuotationId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_ServiceOrderId_DiagnosisRecommendationId",
                table: "QuotationDetails",
                columns: new[] { "ServiceOrderId", "DiagnosisRecommendationId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_UpdatedBy",
                table: "QuotationDetails",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_AppliedBy",
                table: "Quotations",
                column: "AppliedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_ApprovedBy",
                table: "Quotations",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_CreatedBy",
                table: "Quotations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_DiagnosisId",
                table: "Quotations",
                column: "DiagnosisId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_ServiceAdvisorId",
                table: "Quotations",
                column: "ServiceAdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_ServiceOrderId_Status_CreatedAt",
                table: "Quotations",
                columns: new[] { "ServiceOrderId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_ServiceOrderId_VersionNo",
                table: "Quotations",
                columns: new[] { "ServiceOrderId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_SourceSnapshotId",
                table: "Quotations",
                column: "SourceSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_UpdatedBy",
                table: "Quotations",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Quotations_Code",
                table: "Quotations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_salary_advances_approved_by",
                table: "salary_advances",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_salary_advances_deduct_from_payroll_period_id",
                table: "salary_advances",
                column: "deduct_from_payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_salary_advances_staff_id",
                table: "salary_advances",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceBOMs_CreateBy",
                table: "ServiceBOMs",
                column: "CreateBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceBOMs_ProductId",
                table: "ServiceBOMs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceBOMs_ServiceId",
                table: "ServiceBOMs",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceBOMs_UpdateBy",
                table: "ServiceBOMs",
                column: "UpdateBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceBOMs_VehicleModelId",
                table: "ServiceBOMs",
                column: "VehicleModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderInsuranceAssets_ServiceOrder_Type_Order",
                table: "ServiceOrderInsuranceAssets",
                columns: new[] { "ServiceOrderId", "AssetType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderInsuranceUploads_ServiceOrderId",
                table: "ServiceOrderInsuranceUploads",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderInsuranceUploads_Status_CreatedAt",
                table: "ServiceOrderInsuranceUploads",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_contracts_staff_id",
                table: "staff_contracts",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_salary_profiles_branch_id",
                table: "staff_salary_profiles",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_salary_profiles_staff_id",
                table: "staff_salary_profiles",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_salary_profiles_updated_by",
                table: "staff_salary_profiles",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_LeaderId",
                table: "Teams",
                column: "LeaderId");

            migrationBuilder.CreateIndex(
                name: "UQ_Teams_BranchId_Code",
                table: "Teams",
                columns: new[] { "BranchId", "Code" },
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalProposalSnapshotDetails_SnapshotId",
                table: "TechnicalProposalSnapshotDetails",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalProposalSnapshots_DiagnosisId",
                table: "TechnicalProposalSnapshots",
                column: "DiagnosisId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalProposalSnapshots_ServiceOrderId",
                table: "TechnicalProposalSnapshots",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetEntries_ApprovedBy",
                table: "TimesheetEntries",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetEntries_BranchId",
                table: "TimesheetEntries",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetEntries_StaffId",
                table: "TimesheetEntries",
                column: "StaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_GroupSessionId",
                table: "Appointments",
                column: "GroupSessionId",
                principalTable: "GroupSessions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_InsuranceCompanyId",
                table: "Appointments",
                column: "InsuranceCompanyId",
                principalTable: "InsuranceCompanies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DiagnoseRecommendations_DiagnosisAssignments",
                table: "DiagnoseRecommendations",
                column: "DiagnosisAssignmentId",
                principalTable: "DiagnosisAssignments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSessions_BranchId",
                table: "GroupSessions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSessions_CreatedBy",
                table: "GroupSessions",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSessions_CustomerId",
                table: "GroupSessions",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSessions_UpdatedBy",
                table: "GroupSessions",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QC_Inspections_QcChecklistTemplates_TemplateId",
                table: "QC_Inspections",
                column: "TemplateId",
                principalTable: "QcChecklistTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrderDetails_QuotationDetails",
                table: "ServiceOrderDetails",
                column: "QuotationDetailId",
                principalTable: "QuotationDetails",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Diagn__47C69FAC",
                table: "ServiceOrderDetails",
                column: "DiagnosisRecommendationId",
                principalTable: "DiagnoseRecommendations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrders_GroupSessionId",
                table: "ServiceOrders",
                column: "GroupSessionId",
                principalTable: "GroupSessions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Staffs_TeamId",
                table: "Staffs",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSlots_AppointmentId",
                table: "WorkSlots",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSlots_ServiceOrderId",
                table: "WorkSlots",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");
            migrationBuilder.Sql("ENABLE TRIGGER ALL ON DATABASE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DISABLE TRIGGER ALL ON DATABASE;");
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_GroupSessionId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_InsuranceCompanyId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_DiagnoseRecommendations_DiagnosisAssignments",
                table: "DiagnoseRecommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupSessions_BranchId",
                table: "GroupSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupSessions_CreatedBy",
                table: "GroupSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupSessions_CustomerId",
                table: "GroupSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupSessions_UpdatedBy",
                table: "GroupSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_QC_Inspections_QcChecklistTemplates_TemplateId",
                table: "QC_Inspections");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrderDetails_QuotationDetails",
                table: "ServiceOrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__Diagn__47C69FAC",
                table: "ServiceOrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrders_GroupSessionId",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_Staffs_TeamId",
                table: "Staffs");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSlots_AppointmentId",
                table: "WorkSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSlots_ServiceOrderId",
                table: "WorkSlots");

            migrationBuilder.DropTable(
                name: "AI_FraudMonitors");

            migrationBuilder.DropTable(
                name: "AppointmentInsuranceAssets");

            migrationBuilder.DropTable(
                name: "AppointmentInsuranceUploads");

            migrationBuilder.DropTable(
                name: "attendance_adjustments");

            migrationBuilder.DropTable(
                name: "BayAllocations");

            migrationBuilder.DropTable(
                name: "BayStatusLogs");

            migrationBuilder.DropTable(
                name: "CapacityConfigs");

            migrationBuilder.DropTable(
                name: "ConsumableReturnBundleItems");

            migrationBuilder.DropTable(
                name: "DiagnosisAssignments");

            migrationBuilder.DropTable(
                name: "employee_dependents");

            migrationBuilder.DropTable(
                name: "employee_loans");

            migrationBuilder.DropTable(
                name: "MandatoryOverrideRequests");

            migrationBuilder.DropTable(
                name: "overtime_entries");

            migrationBuilder.DropTable(
                name: "PackageItems");

            migrationBuilder.DropTable(
                name: "PartInventoryReservations");

            migrationBuilder.DropTable(
                name: "payslips");

            migrationBuilder.DropTable(
                name: "pit_profiles");

            migrationBuilder.DropTable(
                name: "QcChecklistItems");

            migrationBuilder.DropTable(
                name: "QuotationAccessLinks");

            migrationBuilder.DropTable(
                name: "salary_advances");

            migrationBuilder.DropTable(
                name: "ServiceBOMs");

            migrationBuilder.DropTable(
                name: "ServiceOrderInsuranceAssets");

            migrationBuilder.DropTable(
                name: "ServiceOrderInsuranceUploads");

            migrationBuilder.DropTable(
                name: "staff_contracts");

            migrationBuilder.DropTable(
                name: "staff_salary_profiles");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "TechnicalProposalSnapshotDetails");

            migrationBuilder.DropTable(
                name: "TimesheetEntries");

            migrationBuilder.DropTable(
                name: "ConsumableReturnBundles");

            migrationBuilder.DropTable(
                name: "QuotationDetails");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "payroll_items");

            migrationBuilder.DropTable(
                name: "QcChecklistTemplates");

            migrationBuilder.DropTable(
                name: "Quotations");

            migrationBuilder.DropTable(
                name: "payroll_runs");

            migrationBuilder.DropTable(
                name: "TechnicalProposalSnapshots");

            migrationBuilder.DropTable(
                name: "payroll_periods");

            migrationBuilder.DropIndex(
                name: "IX_WorkSlots_AppointmentId",
                table: "WorkSlots");

            migrationBuilder.DropIndex(
                name: "IX_WorkSlots_ServiceOrderId",
                table: "WorkSlots");

            migrationBuilder.DropIndex(
                name: "IX_Staffs_TeamId",
                table: "Staffs");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrders_GroupSessionId",
                table: "ServiceOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceOrders_OrderCategory",
                table: "ServiceOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceOrders_Status",
                table: "ServiceOrders");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrderDetails_DiagnosisRecommendationId",
                table: "ServiceOrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_QC_Inspections_TemplateId",
                table: "QC_Inspections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GroupSessions_Status",
                table: "GroupSessions");

            migrationBuilder.DropIndex(
                name: "IX_DiagnoseRecommendations_DiagnosisAssignmentId",
                table: "DiagnoseRecommendations");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_InsuranceCompanyId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "WorkSlots");

            migrationBuilder.DropColumn(
                name: "IsAdditional",
                table: "WorkSlots");

            migrationBuilder.DropColumn(
                name: "SequenceOrder",
                table: "WorkSlots");

            migrationBuilder.DropColumn(
                name: "ServiceOrderId",
                table: "WorkSlots");

            migrationBuilder.DropColumn(
                name: "VehicleClassId",
                table: "VehicleModels");

            migrationBuilder.DropColumn(
                name: "EngineType",
                table: "StandardServices");

            migrationBuilder.DropColumn(
                name: "GoogleId",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "Specialty",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "ProofImageUrl",
                table: "ServiceStaffAssignments");

            migrationBuilder.DropColumn(
                name: "RequiresInsuranceProof",
                table: "ServiceStaffAssignments");

            migrationBuilder.DropColumn(
                name: "GroupSessionId",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "InsuranceClaimMode",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "InsuranceDeductibleAmount",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "InsuranceDeductibleOption",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "InsuranceQuoteState",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "RequestedPackageIds",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "DiagnosisRecommendationId",
                table: "ServiceOrderDetails");

            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "ServiceOrderDetails");

            migrationBuilder.DropColumn(
                name: "InspectedAt",
                table: "QC_Inspections");

            migrationBuilder.DropColumn(
                name: "OverallScore",
                table: "QC_Inspections");

            migrationBuilder.DropColumn(
                name: "ReInspectionCount",
                table: "QC_Inspections");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "QC_Inspections");

            migrationBuilder.DropColumn(
                name: "ConsumableThresholdAmount",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsConsumable",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MinStockLevel",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReorderPoint",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AssignmentNote",
                table: "Diagnoses");

            migrationBuilder.DropColumn(
                name: "DispatchTag",
                table: "Diagnoses");

            migrationBuilder.DropColumn(
                name: "IsEscalated",
                table: "Diagnoses");

            migrationBuilder.DropColumn(
                name: "SlaDueAt",
                table: "Diagnoses");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Diagnoses");

            migrationBuilder.DropColumn(
                name: "DiagnosisAssignmentId",
                table: "DiagnoseRecommendations");

            migrationBuilder.DropColumn(
                name: "RecommendationStatus",
                table: "DiagnoseRecommendations");

            migrationBuilder.DropColumn(
                name: "SoftDeletedAt",
                table: "DiagnoseRecommendations");

            migrationBuilder.DropColumn(
                name: "SoftDeletedBy",
                table: "DiagnoseRecommendations");

            migrationBuilder.DropColumn(
                name: "LastStatusChangedAt",
                table: "Bays");

            migrationBuilder.DropColumn(
                name: "ReservationStatus",
                table: "Bays");

            migrationBuilder.DropColumn(
                name: "SequenceOrder",
                table: "Bays");

            migrationBuilder.DropColumn(
                name: "InsuranceClaimMode",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "InsuranceCompanyId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "InsuranceDeductibleAmount",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "InsuranceDeductibleOption",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "InsuranceQuoteState",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RequestedPackageIds",
                table: "Appointments");

            migrationBuilder.RenameColumn(
                name: "ArrivalTime",
                table: "ServiceOrders",
                newName: "ValidUntil");

            migrationBuilder.RenameColumn(
                name: "QuotationDetailId",
                table: "ServiceOrderDetails",
                newName: "DiagnoseRecommendationId");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceOrderDetails_QuotationDetailId",
                table: "ServiceOrderDetails",
                newName: "IX_ServiceOrderDetails_DiagnoseRecommendationId");

            migrationBuilder.RenameIndex(
                name: "UQ_GroupSessions_GroupCode",
                table: "GroupSessions",
                newName: "UQ_BookingGroups_GroupCode");

            migrationBuilder.RenameIndex(
                name: "IX_GroupSessions_Status_CreatedAt",
                table: "GroupSessions",
                newName: "IX_BookingGroups_Status_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_GroupSessions_CustomerId_CreatedAt",
                table: "GroupSessions",
                newName: "IX_BookingGroups_CustomerId_CreatedAt");

            migrationBuilder.RenameColumn(
                name: "DiagnosisId",
                table: "DiagnoseRecommendations",
                newName: "DiagnoseId");

            migrationBuilder.RenameIndex(
                name: "IX_DiagnoseRecommendations_DiagnosisId",
                table: "DiagnoseRecommendations",
                newName: "IX_DiagnoseRecommendations_DiagnoseId");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_GroupSessionId",
                table: "Appointments",
                newName: "IX_Appointments_BookingGroupId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WorkSlots",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "AVAILABLE",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldDefaultValue: "RESERVED");

            migrationBuilder.AlterColumn<long>(
                name: "BayId",
                table: "WorkSlots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Segment",
                table: "VehicleModels",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FuelType",
                table: "VehicleModels",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CylinderCapacity",
                table: "VehicleModels",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VehicleClassId",
                table: "StandardServices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "ProductImages",
                type: "varchar(500)",
                unicode: false,
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(MAX)");

            migrationBuilder.AddColumn<long>(
                name: "ApprovedByForemanId",
                table: "Diagnoses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TechnicianId",
                table: "Diagnoses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_ApprovedByForemanId",
                table: "Diagnoses",
                column: "ApprovedByForemanId");

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_TechnicianId",
                table: "Diagnoses",
                column: "TechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_BookingGroupId",
                table: "Appointments",
                column: "GroupSessionId",
                principalTable: "GroupSessions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Diagnoses__Appro__61BB7BD9",
                table: "Diagnoses",
                column: "ApprovedByForemanId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Diagnoses__Techn__60C757A0",
                table: "Diagnoses",
                column: "TechnicianId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingGroups_BranchId",
                table: "GroupSessions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingGroups_CreatedBy",
                table: "GroupSessions",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingGroups_CustomerId",
                table: "GroupSessions",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingGroups_UpdatedBy",
                table: "GroupSessions",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Diagn__47C69FAC",
                table: "ServiceOrderDetails",
                column: "DiagnoseRecommendationId",
                principalTable: "DiagnoseRecommendations",
                principalColumn: "Id");
            migrationBuilder.Sql("ENABLE TRIGGER ALL ON DATABASE;");
        }
    }
}
