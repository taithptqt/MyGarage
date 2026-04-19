using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGarage.Migrations
{
    /// <inheritdoc />
    public partial class SeedStoreKeeperRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "AI_KnowledgeBase",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Topic = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    IsDynamic = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AI_Knowl__3214EC0754A71E47", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AI_ModelConfigs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ApiKey = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    ModelName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    MaxTokens = table.Column<int>(type: "int", nullable: true),
                    Temperature = table.Column<decimal>(type: "decimal(3,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AI_Model__3214EC07716BF4FE", x => x.Id);
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
                name: "SalaryCoefficients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YearsOfService = table.Column<int>(type: "int", nullable: true),
                    CoefficientName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoefficientValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryCoefficients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AI_Forecasts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PredictedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AI_Forec__3214EC07A43540EC", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AI_SmartDispatching",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EfficiencyScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "GENERATED"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AI_Smart__3214EC07DC395169", x => x.Id);
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
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    VehicleId = table.Column<long>(type: "bigint", nullable: true),
                    GroupSessionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    RequestedServiceIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedPackageIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InsuranceCompanyId = table.Column<long>(type: "bigint", nullable: true),
                    InsuranceDeductibleAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InsuranceDeductibleOption = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    InsuranceClaimMode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    InsuranceQuoteState = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    CustomerNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsReminderSent = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Appointm__3214EC0762F6D2AE", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ServiceOrderDiscountId = table.Column<long>(type: "bigint", nullable: true),
                    GatePassId = table.Column<long>(type: "bigint", nullable: true),
                    MaterialReturnId = table.Column<long>(type: "bigint", nullable: true),
                    RequestedBy = table.Column<long>(type: "bigint", nullable: false),
                    ApproverId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true, defaultValue: "PENDING"),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Approval__3214EC07BCDC6900", x => x.Id);
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
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    UserType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TableName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    RecordId = table.Column<long>(type: "bigint", nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SessionId = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AuditLog__3214EC0770616275", x => x.Id);
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
                });

            migrationBuilder.CreateTable(
                name: "Bays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SequenceOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    BayType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ServiceArea = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    MaxTonnage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    OperationalStatus = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "ACTIVE"),
                    CurrentOccupiedVehicleId = table.Column<long>(type: "bigint", nullable: true),
                    LastStatusChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReservationStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK__Bays__3214EC07708F9CE3", x => x.Id);
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
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Lat = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                    Lng = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    Hotline = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ManagerId = table.Column<long>(type: "bigint", nullable: true),
                    Email = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "ACTIVE"),
                    TaxCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    MaxWorkSlots = table.Column<int>(type: "int", nullable: true),
                    AvatarUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK__Branches__3214EC0712918BB8", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BranchQuotationApprovalPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    MaxDiscountPercentWithoutManager = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 5m),
                    HighValueThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 50000000m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchQuotationApprovalPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchQuotationApprovalPolicies_Branches",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrnDailySnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GrnCount = table.Column<int>(type: "int", nullable: false),
                    TotalReceivedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalReceivedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    DistinctProducts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrnDailySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrnDailySnapshots_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevenueSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TotalCollected = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TotalDebt = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TotalExpense = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    NetProfit = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    NetCashFlow = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RevenueS__3214EC07FCDD558C", x => x.Id);
                    table.ForeignKey(
                        name: "FK__RevenueSn__Branc__3C54ED00",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    ShiftName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Shifts__3214EC0704926738", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Shifts__BranchId__69279377",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BranchExceptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    ExceptionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedByStaffId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedByStaffId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__BranchEx__3214EC0719CB714B", x => x.Id);
                    table.ForeignKey(
                        name: "FK__BranchExc__Branc__2D47B39A",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BranchOperatingHours",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    CloseTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsDayOff = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    SlotDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxConcurrentBookings = table.Column<int>(type: "int", nullable: false),
                    UpdatedByStaffId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__BranchOp__3214EC07A3A269AF", x => x.Id);
                    table.ForeignKey(
                        name: "FK__BranchOpe__Branc__30242045",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
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
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    IsDepositRequired = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK__Categori__3214EC0799C9DC1C", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Categorie__Paren__320C68B7",
                        column: x => x.ParentId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatbotActionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    ActionType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "SUCCESS"),
                    ActionPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotActionLogs", x => x.Id);
                    table.CheckConstraint("CK_ChatbotActionLogs_ActionPayload_IsJson", "[ActionPayload] IS NULL OR ISJSON([ActionPayload]) = 1");
                    table.CheckConstraint("CK_ChatbotActionLogs_Status", "[Status] IN ('PENDING','SUCCESS','FAILED','SKIPPED')");
                });

            migrationBuilder.CreateTable(
                name: "ChatbotMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IntentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SentimentScore = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    ToolName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ToolArgs = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToolResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotMessages", x => x.Id);
                    table.CheckConstraint("CK_ChatbotMessages_Role", "[Role] IN ('USER','ASSISTANT','SYSTEM','TOOL_CALL','TOOL_RESULT')");
                    table.CheckConstraint("CK_ChatbotMessages_SentimentScore", "[SentimentScore] IS NULL OR ([SentimentScore] >= CAST(-1.0000 AS decimal(5,4)) AND [SentimentScore] <= CAST(1.0000 AS decimal(5,4)))");
                    table.CheckConstraint("CK_ChatbotMessages_ToolArgs_IsJson", "[ToolArgs] IS NULL OR ISJSON([ToolArgs]) = 1");
                    table.CheckConstraint("CK_ChatbotMessages_ToolResult_IsJson", "[ToolResult] IS NULL OR ISJSON([ToolResult]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "ChatbotSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionToken = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentIntent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IntentState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContextVehicleId = table.Column<long>(type: "bigint", nullable: true),
                    ContextServiceCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    HandoffConversationId = table.Column<long>(type: "bigint", nullable: true),
                    HandoffSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotSessions", x => x.Id);
                    table.CheckConstraint("CK_ChatbotSessions_HandoffSummary_IsJson", "[HandoffSummary] IS NULL OR ISJSON([HandoffSummary]) = 1");
                    table.CheckConstraint("CK_ChatbotSessions_IntentState_IsJson", "[IntentState] IS NULL OR ISJSON([IntentState]) = 1");
                    table.CheckConstraint("CK_ChatbotSessions_SessionToken_NotEmpty", "LEN([SessionToken]) > 0");
                    table.CheckConstraint("CK_ChatbotSessions_Status", "[Status] IN ('ACTIVE','IDLE','HANDED_OFF','EXPIRED','CLOSED','ARCHIVED')");
                    table.ForeignKey(
                        name: "FK_ChatbotSessions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessageAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    AttachmentUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageAttachments", x => x.Id);
                    table.CheckConstraint("CK_ChatMessageAttachments_SortOrder", "[SortOrder] >= (1)");
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SenderId = table.Column<long>(type: "bigint", nullable: false),
                    SenderType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    MessageType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true, defaultValue: "TEXT"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ChatMess__3214EC07A1C55ACB", x => x.Id);
                    table.CheckConstraint("CK_ChatMessages_MessageType", "[MessageType] IN ('TEXT','IMAGE','SYSTEM')");
                    table.CheckConstraint("CK_ChatMessages_SenderType", "[SenderType] IN ('CUSTOMER','STAFF','SYSTEM')");
                });

            migrationBuilder.CreateTable(
                name: "CommissionRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    TargetType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: true),
                    StaffTierId = table.Column<long>(type: "bigint", nullable: false),
                    CommissionType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CommissionValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK__Commissi__3214EC07042111C8", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Commissio__Branc__3AA1AEB8",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommissionTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceStaffAssignmentId = table.Column<long>(type: "bigint", nullable: true),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CommissionRuleId = table.Column<long>(type: "bigint", nullable: true),
                    SourceType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliedCommissionType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    AppliedCommissionValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliedSplitRatio = table.Column<decimal>(type: "decimal(5,2)", nullable: true, defaultValue: 100m),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LogType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    OriginalCommissionId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceRefundId = table.Column<long>(type: "bigint", nullable: true),
                    RetailReturnId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true, defaultValue: "PENDING"),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Commissi__3214EC07C2FCF5BE", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Commissio__Origi__3C89F72A",
                        column: x => x.OriginalCommissionId,
                        principalTable: "CommissionTransactions",
                        principalColumn: "Id");
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
                });

            migrationBuilder.CreateTable(
                name: "ConversationCustomerReads",
                columns: table => new
                {
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    LastReadMessageId = table.Column<long>(type: "bigint", nullable: true),
                    LastReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationCustomerReads", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK_ConversationCustomerReads_LastReadMessageId",
                        column: x => x.LastReadMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    VehicleId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedStaffId = table.Column<long>(type: "bigint", nullable: true),
                    LastMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastMessageId = table.Column<long>(type: "bigint", nullable: true),
                    LastSenderId = table.Column<long>(type: "bigint", nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "OPEN"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Conversa__3214EC073AA82895", x => x.Id);
                    table.CheckConstraint("CK_Conversations_Status", "[Status] IN ('OPEN','WAITING_FOR_STAFF','WAITING_FOR_CUSTOMER','CLOSED')");
                    table.ForeignKey(
                        name: "FK_Conversations_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Conversations_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Conversations_LastMessageId",
                        column: x => x.LastMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ConversationStaffReads",
                columns: table => new
                {
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    StaffId = table.Column<long>(type: "bigint", nullable: false),
                    LastReadMessageId = table.Column<long>(type: "bigint", nullable: true),
                    LastReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationStaffReads", x => new { x.ConversationId, x.StaffId });
                    table.ForeignKey(
                        name: "FK_ConversationStaffReads_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationStaffReads_LastReadMessageId",
                        column: x => x.LastReadMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CustomerFeedbacks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Customer__3214EC07F1754D23", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PasswordHash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    TierId = table.Column<long>(type: "bigint", nullable: true),
                    TotalSpending = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    NoShowCount = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SecurityStamp = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    IsLockedOut = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    LockoutEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    GoogleId = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "GUEST")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Customer__3214EC071C60D783", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    LinkAction = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Notifica__3214EC0731481BF1", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Notificat__Custo__0E8E2250",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CustomerTiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinSpending = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Benefits = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK__Customer__3214EC07163563D7", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerVehicles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    VehicleId = table.Column<long>(type: "bigint", nullable: false),
                    IsCurrentOwner = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    OwnershipStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK__Customer__3214EC07D8CEE2BC", x => x.Id);
                    table.ForeignKey(
                        name: "FK__CustomerV__Custo__4EA8A765",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CustomerVouchers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Customer__3214EC0716F1AF48", x => x.Id);
                    table.ForeignKey(
                        name: "FK__CustomerV__Custo__536D5C82",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ShippingAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrackingCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DeliveryEvidenceUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "PENDING"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Deliveri__3214EC070B1DD92B", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Diagnoses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    Symptom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Severity = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "PENDING"),
                    AssignmentNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaDueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsEscalated = table.Column<bool>(type: "bit", nullable: false),
                    DispatchTag = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DiagnosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Diagnose__3214EC071BC3DD6E", x => x.Id);
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
                name: "DiagnosisRecommendations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiagnosisId = table.Column<long>(type: "bigint", nullable: false),
                    DiagnosisAssignmentId = table.Column<long>(type: "bigint", nullable: true),
                    ParentRecommendationId = table.Column<long>(type: "bigint", nullable: true),
                    ItemType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    StandardServiceId = table.Column<long>(type: "bigint", nullable: true),
                    InventoryItemId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RecommendationStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SoftDeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    SoftDeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Urgency = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    EvidenceMediaUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Diagnose__3214EC070DEF315F", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosisRecommendations_DiagnosisAssignments",
                        column: x => x.DiagnosisAssignmentId,
                        principalTable: "DiagnosisAssignments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__DiagnoseR__Diagn__5A1A5A11",
                        column: x => x.DiagnosisId,
                        principalTable: "Diagnoses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__DiagnoseR__Paren__5B0E7E4A",
                        column: x => x.ParentRecommendationId,
                        principalTable: "DiagnosisRecommendations",
                        principalColumn: "Id");
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
                        name: "FK_employee_loans_payroll_periods_start_payroll_period_id",
                        column: x => x.start_payroll_period_id,
                        principalTable: "payroll_periods",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ExpenseLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    ShiftClosingId = table.Column<long>(type: "bigint", nullable: true),
                    ExpenseType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StaffId = table.Column<long>(type: "bigint", nullable: true),
                    ProofUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "PENDING"),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ExpenseL__3214EC07B63E3350", x => x.Id);
                    table.ForeignKey(
                        name: "FK__ExpenseLo__Branc__63A3C44B",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GatePass",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ClearanceType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    QrCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    IssuedBy = table.Column<long>(type: "bigint", nullable: false),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldPartsPackingListUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    IsOldPartsChecked = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "ISSUED"),
                    ScannedBy = table.Column<long>(type: "bigint", nullable: true),
                    SecurityCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PassedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__GatePass__3214EC07516211D2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptNoteItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoodsReceiptNoteId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseOrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptNoteItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GrnCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    ReceivedBy = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    TotalItems = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ConfirmedItems = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CanceledItems = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CanceledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CustomerNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__BookingG__3214EC077B6FE4D1", x => x.Id);
                    table.CheckConstraint("CK_BookingGroups_Status", "[Status] IN ('PENDING','CONFIRMED','PARTIAL_CONFIRMED','CANCELED','COMPLETED')");
                    table.CheckConstraint("CK_GroupSessions_ItemCounters", "[TotalItems] >= 0 AND [ConfirmedItems] >= 0 AND [CanceledItems] >= 0 AND [ConfirmedItems] + [CanceledItems] <= [TotalItems]");
                    table.CheckConstraint("CK_GroupSessions_Status", "[Status] IN ('PENDING','CONFIRMED','PARTIAL_CONFIRMED','CANCELED','COMPLETED')");
                    table.ForeignKey(
                        name: "FK_GroupSessions_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GroupSessions_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InsuranceCompanies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TaxCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    CurrentDebt = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
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
                    table.PrimaryKey("PK__Insuranc__3214EC07749DA508", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MinStockLevel = table.Column<decimal>(type: "decimal(18,3)", nullable: true, defaultValue: 0m),
                    MaxStockLevel = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    LocationBin = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ReservedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true, defaultValue: 0m),
                    Current_MAC_Price = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TotalInventoryValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Inventor__3214EC07EA32CA02", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryAuditDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    SystemQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ActualQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    DifferenceQty = table.Column<decimal>(type: "decimal(19,3)", nullable: true, computedColumnSql: "([ActualQty]-[SystemQty])", stored: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Inventor__3214EC07DA0B0DAD", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "DRAFT"),
                    AuditDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Inventor__3214EC07DF0AA8BB", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceServiceOrderDetailId = table.Column<long>(type: "bigint", nullable: true),
                    ItemType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    StandardServiceId = table.Column<long>(type: "bigint", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CostPriceAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitPriceAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", nullable: true, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__InvoiceD__3214EC07BA422D95", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: true),
                    VehicleId = table.Column<long>(type: "bigint", nullable: true),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InvoiceUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CompanyAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceiveEmail = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    TaxCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    SubTotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    PayerType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PayerId = table.Column<long>(type: "bigint", nullable: true),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    PaymentStatus = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "UNPAID"),
                    CreditApprovedByStaffId = table.Column<long>(type: "bigint", nullable: true),
                    CreditNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Invoices__3214EC077603BEDB", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceReminders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    VehicleId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    ReminderType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReminderMileage = table.Column<int>(type: "int", nullable: true),
                    EstimatedServices = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Channel = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "PUSH"),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerResponse = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResultAppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceReminders_Appointments_ResultAppointmentId",
                        column: x => x.ResultAppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MaintenanceReminders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
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
                });

            migrationBuilder.CreateTable(
                name: "MaterialRequestDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RequestedCondition = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    IssuedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true, defaultValue: 0m),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true, defaultValue: 0m),
                    WastedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true, defaultValue: 0m),
                    ServiceOrderDetailId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "PENDING"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Material__3214EC07D90DB7BF", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    TechnicianId = table.Column<long>(type: "bigint", nullable: true),
                    StorekeeperId = table.Column<long>(type: "bigint", nullable: true),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "PENDING"),
                    IsRework = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Material__3214EC07FEAF631C", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialReturns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    MaterialRequestDetailId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    ReturnUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ItemCondition = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ReturnedBy_StaffId = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedBy_StaffId = table.Column<long>(type: "bigint", nullable: true),
                    ReturnTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "PENDING"),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Material__3214EC07BC10D807", x => x.Id);
                    table.ForeignKey(
                        name: "FK__MaterialR__Mater__05F8DC4F",
                        column: x => x.MaterialRequestDetailId,
                        principalTable: "MaterialRequestDetails",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ModelSpecificRules",
                columns: table => new
                {
                    ModelId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    StandardCapacity = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Tolerance = table.Column<decimal>(type: "decimal(10,2)", nullable: true, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ModelSpe__23976D404AEA9849", x => new { x.ModelId, x.ProductId });
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    TriggerEvent = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK__Notifica__3214EC073CB02436", x => x.Id);
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
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    StaffId = table.Column<long>(type: "bigint", nullable: true),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.CheckConstraint("CK_PasswordResetTokens_Principal", "([CustomerId] IS NOT NULL AND [StaffId] IS NULL) OR ([CustomerId] IS NULL AND [StaffId] IS NOT NULL)");
                    table.CheckConstraint("CK_PasswordResetTokens_TokenHash_NotEmpty", "LEN([TokenHash]) > 0");
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MethodName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MethodCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ProviderInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK__PaymentM__3214EC07B07F4781", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftClosingId = table.Column<long>(type: "bigint", nullable: true),
                    CashierId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: true),
                    PayerType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PayerId = table.Column<long>(type: "bigint", nullable: true),
                    MethodId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "COMPLETED"),
                    ReversedPaymentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    Direction = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "INFLOW")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Payments__3214EC07735396A5", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Payments__Invoic__18178C8A",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Payments__Method__19FFD4FC",
                        column: x => x.MethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Payments__Revers__1AF3F935",
                        column: x => x.ReversedPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
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
                        name: "FK_payroll_runs_payroll_periods_payroll_period_id",
                        column: x => x.payroll_period_id,
                        principalTable: "payroll_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                        name: "FK_payslips_payroll_items_payroll_item_id",
                        column: x => x.payroll_item_id,
                        principalTable: "payroll_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    ImageUrl = table.Column<string>(type: "NVARCHAR(MAX)", nullable: false),
                    IsMain = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    SortOrder = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ProductI__3214EC0756CDEDF5", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SKU = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    RetailPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    ServicePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", nullable: true, defaultValue: 0m),
                    WarrantyMonths = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    IsStrictCapacityControl = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    RequireEmptyReturn = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    MinStockLevel = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    ReorderPoint = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    IsConsumable = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    ConsumableThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
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
                    table.PrimaryKey("PK__Products__3214EC07968B91BC", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Products__Catego__1DD065E0",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true, defaultValue: 0m),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Purchase__3214EC071913A1D3", x => x.Id);
                    table.ForeignKey(
                        name: "FK__PurchaseO__Produ__22951AFD",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    DestinationWarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    ApprovedByStaffId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Purchase__3214EC07E8647D31", x => x.Id);
                    table.ForeignKey(
                        name: "FK__PurchaseO__Branc__23893F36",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QC_Inspections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    QC_StaffId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceOrderDetailId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    FailReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OverallNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReworkTechnicianId = table.Column<long>(type: "bigint", nullable: true),
                    TemplateId = table.Column<long>(type: "bigint", nullable: true),
                    OverallScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    InspectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReInspectionCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__QC_Inspe__3214EC07E74328AB", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QC_Inspections_QcChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "QcChecklistTemplates",
                        principalColumn: "Id");
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
                    OtpCodeHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    OtpExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpAttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OtpVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpLastSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationAccessLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuotationApprovalActions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    ApprovalType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Decision = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ActorStaffId = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationApprovalActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuotationDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    SourceQuotationDetailId = table.Column<long>(type: "bigint", nullable: true),
                    ItemType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPriceAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPriceAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(7,4)", nullable: true),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayerType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    InsuranceCompanyId = table.Column<long>(type: "bigint", nullable: true),
                    InsuranceCoveredAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CustomerCoveredAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CanceledBy = table.Column<long>(type: "bigint", nullable: true),
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
                        name: "FK_QuotationDetails_InsuranceCompanies",
                        column: x => x.InsuranceCompanyId,
                        principalTable: "InsuranceCompanies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuotationDetails_SourceQuotationDetail",
                        column: x => x.SourceQuotationDetailId,
                        principalTable: "QuotationDetails",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QuotationDiscounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<long>(type: "bigint", nullable: false),
                    DiscountType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CustomerTierId = table.Column<long>(type: "bigint", nullable: true),
                    VoucherId = table.Column<long>(type: "bigint", nullable: true),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValueType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppliedBy = table.Column<long>(type: "bigint", nullable: false),
                    DiscountCategory = table.Column<int>(type: "int", nullable: true, computedColumnSql: "(case when [DiscountType]='MEMBERSHIP' then (1) when [DiscountType]='MANUAL' OR [DiscountType]='VOUCHER' then (2) end)", stored: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationDiscounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationDiscounts_CustomerTierId",
                        column: x => x.CustomerTierId,
                        principalTable: "CustomerTiers",
                        principalColumn: "Id");
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
                    DiagnosisAssignmentId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    QuotationType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "NORMAL"),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    SubTotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalInsuranceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalCustomerAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    InsuranceChangeType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "NONE"),
                    InsuranceApprovalEvidenceUrl = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "DRAFT"),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ServiceAdvisorId = table.Column<long>(type: "bigint", nullable: true),
                    SourceSnapshotId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicalApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    TechnicalApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicalApprovalStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true, defaultValue: "PENDING"),
                    RequiresFinancialApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FinancialApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    FinancialApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinancialApprovalStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true, defaultValue: "NOT_REQUIRED"),
                    CustomerDecisionBy = table.Column<long>(type: "bigint", nullable: true),
                    CustomerDecisionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerDecisionStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CustomerNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InternalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsAppliedToServiceOrder = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsReadyForSplitBilling = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SplitBillingReadyAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.CheckConstraint("CK_Quotations_InsuranceApprovalEvidence_Required", "[Status] <> 'INSURANCE_APPROVED' OR [TotalInsuranceAmount] <= 0 OR NULLIF(LTRIM(RTRIM([InsuranceApprovalEvidenceUrl])), '') IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Quotations_Diagnoses",
                        column: x => x.DiagnosisId,
                        principalTable: "Diagnoses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Quotations_DiagnosisAssignments",
                        column: x => x.DiagnosisAssignmentId,
                        principalTable: "DiagnosisAssignments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RetailOrderItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CostPriceAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitPriceAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", nullable: true, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RetailOr__3214EC0757E0441D", x => x.Id);
                    table.ForeignKey(
                        name: "FK__RetailOrd__Produ__2D12A970",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RetailOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    OrderCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TierDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    VoucherId = table.Column<long>(type: "bigint", nullable: true),
                    VoucherDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TotalDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "DRAFT"),
                    CustomerNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RetailOr__3214EC07E6EE6099", x => x.Id);
                    table.ForeignKey(
                        name: "FK__RetailOrd__Branc__2E06CDA9",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__RetailOrd__Custo__2EFAF1E2",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RetailReturnItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RetailReturnId = table.Column<long>(type: "bigint", nullable: false),
                    RetailOrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ItemCondition = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true, defaultValue: "PENDING"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConditionEvidenceUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RetailRe__3214EC0700BDCE4A", x => x.Id);
                    table.ForeignKey(
                        name: "FK__RetailRet__Retai__33BFA6FF",
                        column: x => x.RetailOrderItemId,
                        principalTable: "RetailOrderItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RetailReturns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ReturnCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TotalRefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "PENDING"),
                    ReceivedBy = table.Column<long>(type: "bigint", nullable: true),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RetailRe__3214EC07D241738E", x => x.Id);
                    table.ForeignKey(
                        name: "FK__RetailRet__Retai__379037E3",
                        column: x => x.RetailOrderId,
                        principalTable: "RetailOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Scope = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK__Roles__3214EC079FD50E25", x => x.Id);
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
                        name: "FK_salary_advances_payroll_periods_deduct_from_payroll_period_id",
                        column: x => x.deduct_from_payroll_period_id,
                        principalTable: "payroll_periods",
                        principalColumn: "id");
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
                        name: "FK_ServiceBOMs_Product",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ParentCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
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
                    table.PrimaryKey("PK__ServiceC__3214EC07586F8C98", x => x.Id);
                    table.ForeignKey(
                        name: "FK__ServiceCa__Paren__4301EA8F",
                        column: x => x.ParentCategoryId,
                        principalTable: "ServiceCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrderDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    QuotationDetailId = table.Column<long>(type: "bigint", nullable: true),
                    StandardServiceId = table.Column<long>(type: "bigint", nullable: true),
                    TechnicianId = table.Column<long>(type: "bigint", nullable: true),
                    OriginalOrderDetailId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    ItemType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BuyoutCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsWarrantyItem = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    IsCustomerBroughtPart = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: true),
                    IssuedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true, defaultValue: 0m),
                    CostPriceAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitPriceAtTime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    DiscountAllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayerType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    InsuranceCompanyId = table.Column<long>(type: "bigint", nullable: true),
                    InsuranceRequestedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    InsuranceApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    CustomerCoPayAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    InsuranceStatus = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "NOT_APPLICABLE"),
                    GarageAbsorbedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    AbsorbedApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "DRAFT"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsReturnOldPart = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    ServiceSource = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "INITIAL"),
                    IsSupplementary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SupplementaryReasonCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    SourceQuotationId = table.Column<long>(type: "bigint", nullable: true),
                    SupplementaryRequestedByStaffId = table.Column<long>(type: "bigint", nullable: true),
                    SupplementaryRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ServiceO__3214EC07F58DFBE0", x => x.Id);
                    table.CheckConstraint("CK_ServiceOrderDetails_SupplementaryConsistency", "([ServiceSource]='INITIAL' AND [IsSupplementary]=0 AND [SupplementaryReasonCode] IS NULL) OR ([ServiceSource]='SUPPLEMENTARY_TECHNICIAN' AND [IsSupplementary]=1 AND [SupplementaryReasonCode]='TECH_FINDING') OR ([ServiceSource]='SUPPLEMENTARY_CUSTOMER' AND [IsSupplementary]=1 AND [SupplementaryReasonCode]='CUSTOMER_REQUEST')");
                    table.CheckConstraint("CK_ServiceOrderDetails_SupplementaryReasonCode", "[SupplementaryReasonCode] IS NULL OR [SupplementaryReasonCode] IN ('TECH_FINDING','CUSTOMER_REQUEST')");
                    table.ForeignKey(
                        name: "FK_ServiceOrderDetails_QuotationDetails",
                        column: x => x.QuotationDetailId,
                        principalTable: "QuotationDetails",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceOrderDetails_SourceQuotation",
                        column: x => x.SourceQuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__ServiceOr__Insur__4C8B54C9",
                        column: x => x.InsuranceCompanyId,
                        principalTable: "InsuranceCompanies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__ServiceOr__Origi__4AA30C57",
                        column: x => x.OriginalOrderDetailId,
                        principalTable: "ServiceOrderDetails",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrderDiscounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    DiscountType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CustomerTierId = table.Column<long>(type: "bigint", nullable: true),
                    VoucherId = table.Column<long>(type: "bigint", nullable: true),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValueType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppliedBy = table.Column<long>(type: "bigint", nullable: false),
                    DiscountCategory = table.Column<int>(type: "int", nullable: true, computedColumnSql: "(case when [DiscountType]='MEMBERSHIP' then (1) when [DiscountType]='MANUAL' OR [DiscountType]='VOUCHER' then (2)  end)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ServiceO__3214EC07B07AEE3B", x => x.Id);
                    table.ForeignKey(
                        name: "FK__ServiceOr__Custo__4E739D3B",
                        column: x => x.CustomerTierId,
                        principalTable: "CustomerTiers",
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
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    VehicleId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    GroupSessionId = table.Column<long>(type: "bigint", nullable: true),
                    OriginBranchId = table.Column<long>(type: "bigint", nullable: false),
                    ExecuteBranchId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    OrderCategory = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ServiceAdvisorId = table.Column<long>(type: "bigint", nullable: true),
                    ReceptionistId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedPackageIds = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InsuranceDeductibleAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InsuranceDeductibleOption = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    InsuranceClaimMode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    InsuranceQuoteState = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    RevisionNumber = table.Column<int>(type: "int", nullable: true, defaultValue: 1),
                    SubTotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TotalDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    RequiredDepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TotalDepositedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    DepositStatus = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "NOT_REQUIRED"),
                    DepositRefundedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    DepositForfeitedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "NEW"),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QCStatus = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "PENDING"),
                    QCInspectorId = table.Column<long>(type: "bigint", nullable: true),
                    QCVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OldPartsReturnOption = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    CheckInTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArrivalTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentStatus = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "UNPAID"),
                    EstimatedHandover = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PromisedHandoverTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualHandover = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ServiceO__3214EC07A6BCD573", x => x.Id);
                    table.CheckConstraint("CK_ServiceOrders_OrderCategory", "[OrderCategory] IS NULL OR ([OrderCategory]='WARRANTLY' OR [OrderCategory]='REWORK' OR [OrderCategory]='INSURANCE_REPAIR' OR [OrderCategory]='QUICK_SERVICE' OR [OrderCategory]='GENERAL_REPAIR' OR [OrderCategory]='MAINTENANCE')");
                    table.CheckConstraint("CK_ServiceOrders_Status", "[Status] IN ('NEW','CHECKED_IN','WAITING','DIAGNOSING','QUOTING','WAITING_APPROVAL','APPROVED','IN_PROGRESS','REPAIRING','READY','WAITING_PAYMENT','CLOSED','CANCELED')");
                    table.ForeignKey(
                        name: "FK_ServiceOrders_GroupSessionId",
                        column: x => x.GroupSessionId,
                        principalTable: "GroupSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__ServiceOr__Appoi__53385258",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__ServiceOr__Custo__515009E6",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__ServiceOr__Execu__55209ACA",
                        column: x => x.ExecuteBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__ServiceOr__Origi__542C7691",
                        column: x => x.OriginBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__ServiceOr__Origi__5614BF03",
                        column: x => x.OriginalServiceOrderId,
                        principalTable: "ServiceOrders",
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
                name: "ServiceOrderSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    SnapshotData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ServiceO__3214EC0711723175", x => x.Id);
                    table.ForeignKey(
                        name: "FK__ServiceOr__Servi__5BCD9859",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceRefunds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ServiceR__3214EC07E092AD2D", x => x.Id);
                    table.ForeignKey(
                        name: "FK__ServiceRe__Servi__5DB5E0CB",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceStaffAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderDetailId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedBy = table.Column<long>(type: "bigint", nullable: true),
                    TechnicianId = table.Column<long>(type: "bigint", nullable: false),
                    RoleInTask = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    SplitRatio = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 100.00m),
                    EstimatedCommission = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActualStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PauseLog = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActualEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "PENDING"),
                    IsRework = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    RequiresInsuranceProof = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ProofImageUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ServiceS__3214EC074773E670", x => x.Id);
                    table.ForeignKey(
                        name: "FK__ServiceSt__Servi__618671AF",
                        column: x => x.ServiceOrderDetailId,
                        principalTable: "ServiceOrderDetails",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ShiftClosings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    WorkShiftId = table.Column<long>(type: "bigint", nullable: false),
                    CashierId = table.Column<long>(type: "bigint", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCashIn = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TotalCashOut = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    ExpectedBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    ActualBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    Discrepancy = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "OPEN"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ManagerId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ShiftClo__3214EC07117EA13A", x => x.Id);
                    table.ForeignKey(
                        name: "FK__ShiftClos__Branc__65570293",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__ShiftClos__WorkS__664B26CC",
                        column: x => x.WorkShiftId,
                        principalTable: "Shifts",
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
                });

            migrationBuilder.CreateTable(
                name: "Staffs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    GoogleId = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PasswordHash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    TeamId = table.Column<long>(type: "bigint", nullable: true),
                    StaffTierId = table.Column<long>(type: "bigint", nullable: true),
                    SkillLevel = table.Column<int>(type: "int", nullable: true),
                    Specialty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CitizenId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AvatarUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    SecurityStamp = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    IsLockedOut = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    LockoutEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, defaultValue: "ACTIVE"),
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
                    table.PrimaryKey("PK__Staffs__3214EC07674AA95B", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Staffs__BranchId__6B0FDBE9",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Staffs__CreatedB__6CF8245B",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Staffs__DeletedB__6EE06CCD",
                        column: x => x.DeletedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Staffs__RoleId__6A1BB7B0",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Staffs__UpdatedB__6DEC4894",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StaffSchedules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftId = table.Column<long>(type: "bigint", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckOutTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__StaffSch__3214EC0779258F4D", x => x.Id);
                    table.ForeignKey(
                        name: "FK__StaffSche__Shift__70C8B53F",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__StaffSche__Staff__6FD49106",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StaffTiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BaseSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
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
                    table.PrimaryKey("PK__StaffTie__3214EC077A692A60", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffTiers_Staffs_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffTiers_Staffs_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffTiers_Staffs_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StandardServices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EngineType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StandardMinutes = table.Column<int>(type: "int", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", nullable: true, defaultValue: 0m),
                    WarrantyMonths = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    WarrantyKm = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    ConsumableCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
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
                    table.PrimaryKey("PK__Standard__3214EC0745B61617", x => x.Id);
                    table.ForeignKey(
                        name: "FK__StandardS__Categ__74994623",
                        column: x => x.CategoryId,
                        principalTable: "ServiceCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__StandardS__Creat__758D6A5C",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__StandardS__Delet__7775B2CE",
                        column: x => x.DeletedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__StandardS__Updat__76818E95",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: true),
                    RetailReturnId = table.Column<long>(type: "bigint", nullable: true),
                    MaterialRequestId = table.Column<long>(type: "bigint", nullable: true),
                    MaterialReturnId = table.Column<long>(type: "bigint", nullable: true),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__StockTra__3214EC07104DE41B", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransactions_MaterialReturnId",
                        column: x => x.MaterialReturnId,
                        principalTable: "MaterialReturns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockTransactions_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockTransactions_RetailOrderId",
                        column: x => x.RetailOrderId,
                        principalTable: "RetailOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockTransactions_RetailReturnId",
                        column: x => x.RetailReturnId,
                        principalTable: "RetailReturns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__StockTran__Creat__795DFB40",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__StockTran__Produ__7869D707",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrentDebt = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    TaxCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK__Supplier__3214EC077AD2834E", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Suppliers__Creat__7E22B05D",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Suppliers__Delet__000AF8CF",
                        column: x => x.DeletedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Suppliers__Updat__7F16D496",
                        column: x => x.UpdatedBy,
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
                name: "TimesheetEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClockIn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClockOut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BreakMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TaskType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
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
                name: "VehicleBrands",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LogoUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    CountryOfOrigin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK__VehicleB__3214EC07EADA13FE", x => x.Id);
                    table.ForeignKey(
                        name: "FK__VehicleBr__Creat__00FF1D08",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__VehicleBr__Delet__02E7657A",
                        column: x => x.DeletedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__VehicleBr__Updat__01F34141",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VehicleConditionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    LogType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    OdoReading = table.Column<int>(type: "int", nullable: true),
                    FuelLevel = table.Column<byte>(type: "tinyint", nullable: true),
                    DashboardWarningLights = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Valuables = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DefectsAndImages = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerSignatureUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    InspectorId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__VehicleC__3214EC075745CAC4", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleConditionLogs_Staffs_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__VehicleCo__Creat__05C3D225",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__VehicleCo__Inspe__04CFADEC",
                        column: x => x.InspectorId,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__VehicleCo__Servi__03DB89B3",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VoucherCampaigns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BudgetLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BudgetUsed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    OwnerStaffId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherCampaigns_Staffs_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherCampaigns_Staffs_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherCampaigns_Staffs_OwnerStaffId",
                        column: x => x.OwnerStaffId,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherCampaigns_Staffs_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ManagerId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK__Warehous__3214EC07FE923BC8", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Warehouse__Branc__131DCD43",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Warehouse__Creat__150615B5",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Warehouse__Delet__16EE5E27",
                        column: x => x.DeletedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Warehouse__Manag__1411F17C",
                        column: x => x.ManagerId,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Warehouse__Updat__15FA39EE",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VehicleModels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrandId = table.Column<long>(type: "bigint", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Segment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FuelType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CylinderCapacity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VehicleClassId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK__VehicleM__3214EC0789BEED56", x => x.Id);
                    table.ForeignKey(
                        name: "FK__VehicleMo__Brand__08A03ED0",
                        column: x => x.BrandId,
                        principalTable: "VehicleBrands",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__VehicleMo__Creat__09946309",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__VehicleMo__Delet__0B7CAB7B",
                        column: x => x.DeletedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__VehicleMo__Updat__0A888742",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinOrderValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsageLimit = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    UsedCount = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    MaxDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsStackable = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    ValidFrom = table.Column<TimeOnly>(type: "time", nullable: true),
                    ValidTo = table.Column<TimeOnly>(type: "time", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    VoucherName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CampaignId = table.Column<long>(type: "bigint", nullable: true),
                    MaxUsesPerCustomer = table.Column<int>(type: "int", nullable: false),
                    MaxUsesPerVehicle = table.Column<int>(type: "int", nullable: false),
                    IssuedCount = table.Column<int>(type: "int", nullable: false),
                    RedeemedCount = table.Column<int>(type: "int", nullable: false),
                    BudgetLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BudgetUsed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FirstUseOnly = table.Column<bool>(type: "bit", nullable: false),
                    ExcludeDiscountedItems = table.Column<bool>(type: "bit", nullable: false),
                    AllowedPaymentMethods = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllowedChannels = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidDaysOfWeek = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Timezone = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ApprovalRequired = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Vouchers__3214EC070B87BA0F", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vouchers_VoucherCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "VoucherCampaigns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Vouchers__Create__10416098",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Vouchers__Delete__1229A90A",
                        column: x => x.DeletedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Vouchers__Update__113584D1",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    AlertType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CurrentStock = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MinStock = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByPurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAlerts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAlerts_PurchaseOrders_ResolvedByPurchaseOrderId",
                        column: x => x.ResolvedByPurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockAlerts_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LicensePlate = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    VIN = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ModelId = table.Column<long>(type: "bigint", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ODO = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    OdoLastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManufactureYear = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK__Vehicles__3214EC0730F8DFC4", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Vehicles__Create__0D64F3ED",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Vehicles__Delete__0F4D3C5F",
                        column: x => x.DeletedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Vehicles__ModelI__0C70CFB4",
                        column: x => x.ModelId,
                        principalTable: "VehicleModels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Vehicles__Update__0E591826",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VoucherAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    AssignmentType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: false),
                    IsInclude = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherAssignments_Staffs_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherAssignments_Staffs_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherAssignments_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VoucherAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherId = table.Column<long>(type: "bigint", nullable: true),
                    EntityName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ReasonCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    BeforeData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedBy = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherAuditLogs_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherAuditLogs_Staffs_PerformedBy",
                        column: x => x.PerformedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherAuditLogs_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VoucherCodeBatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    BatchCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    SourceType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SuccessCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DuplicateCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InvalidCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherCodeBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherCodeBatches_Staffs_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherCodeBatches_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VoucherConflictRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    ConflictType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ConflictTarget = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActionType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MessageCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    MessageText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherConflictRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherConflictRules_Staffs_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherConflictRules_Staffs_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherConflictRules_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VoucherUsageRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    RuleType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Operator = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RuleValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsExclude = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HardBlock = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherUsageRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherUsageRules_Staffs_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherUsageRules_Staffs_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherUsageRules_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VehicleInsurances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<long>(type: "bigint", nullable: false),
                    InsuranceCompanyId = table.Column<long>(type: "bigint", nullable: false),
                    PolicyNumber = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__VehicleI__3214EC07269DD274", x => x.Id);
                    table.ForeignKey(
                        name: "FK__VehicleIn__Insur__07AC1A97",
                        column: x => x.InsuranceCompanyId,
                        principalTable: "InsuranceCompanies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__VehicleIn__Vehic__06B7F65E",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkSlots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentSlotId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    BayId = table.Column<long>(type: "bigint", nullable: true),
                    TechnicianId = table.Column<long>(type: "bigint", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlotType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    VehicleId = table.Column<long>(type: "bigint", nullable: true),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    IsAdditional = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SequenceOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ServiceStaffAssignmentId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "RESERVED"),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualReleaseTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__WorkSlot__3214EC0774196CAC", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSlots_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkSlots_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__WorkSlots__BayId__19CACAD2",
                        column: x => x.BayId,
                        principalTable: "Bays",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__WorkSlots__Branc__18D6A699",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__WorkSlots__Custo__1BB31344",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__WorkSlots__Paren__17E28260",
                        column: x => x.ParentSlotId,
                        principalTable: "WorkSlots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__WorkSlots__Techn__1ABEEF0B",
                        column: x => x.TechnicianId,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__WorkSlots__Vehic__1CA7377D",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VoucherApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestType = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    VoucherId = table.Column<long>(type: "bigint", nullable: true),
                    RedemptionId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    RequestedBy = table.Column<long>(type: "bigint", nullable: false),
                    ApproverId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RiskLevel = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RejectReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImpactAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SLADeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherApprovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherApprovalRequests_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherApprovalRequests_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherApprovalRequests_RetailOrders_RetailOrderId",
                        column: x => x.RetailOrderId,
                        principalTable: "RetailOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherApprovalRequests_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherApprovalRequests_Staffs_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherApprovalRequests_Staffs_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherApprovalRequests_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VoucherRedemptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    VoucherCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    VehicleId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    RetailOrderId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentMethodId = table.Column<long>(type: "bigint", nullable: true),
                    ValidationStatus = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    RedemptionStatus = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    FailReasonCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    EligibleAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BudgetImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReverseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_RetailOrders_RetailOrderId",
                        column: x => x.RetailOrderId,
                        principalTable: "RetailOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_Staffs_AppliedBy",
                        column: x => x.AppliedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_Staffs_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Staffs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_VoucherApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalTable: "VoucherApprovalRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VoucherRedemptions_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VoucherApprovalRequestVoucherRedemption",
                columns: table => new
                {
                    VoucherApprovalRequestsId = table.Column<long>(type: "bigint", nullable: false),
                    VoucherRedemptionsId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherApprovalRequestVoucherRedemption", x => new { x.VoucherApprovalRequestsId, x.VoucherRedemptionsId });
                    table.ForeignKey(
                        name: "FK_VoucherApprovalRequestVoucherRedemption_VoucherApprovalRequests_VoucherApprovalRequestsId",
                        column: x => x.VoucherApprovalRequestsId,
                        principalTable: "VoucherApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoucherApprovalRequestVoucherRedemption_VoucherRedemptions_VoucherRedemptionsId",
                        column: x => x.VoucherRedemptionsId,
                        principalTable: "VoucherRedemptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "IsActive", "IsDeleted", "RoleCode", "RoleName", "Scope", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 3L, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1L, null, null, "Quản lý vật tư, phụ tùng và điều phối kho.", true, false, "STORE_KEEPER", "Thủ kho", "WAREHOUSE", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.CreateIndex(
                name: "IX_AI_Forecasts_VehicleId",
                table: "AI_Forecasts",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_AI_SmartDispatching_BranchId",
                table: "AI_SmartDispatching",
                column: "BranchId");

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
                name: "IX_Appointments_BranchId",
                table: "Appointments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CreatedBy",
                table: "Appointments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerId",
                table: "Appointments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_GroupSessionId",
                table: "Appointments",
                column: "GroupSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_InsuranceCompanyId",
                table: "Appointments",
                column: "InsuranceCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_UpdatedBy",
                table: "Appointments",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_VehicleId",
                table: "Appointments",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_ApproverId",
                table: "ApprovalRequests",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_GatePassId",
                table: "ApprovalRequests",
                column: "GatePassId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_MaterialReturnId",
                table: "ApprovalRequests",
                column: "MaterialReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_RequestedBy",
                table: "ApprovalRequests",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_ServiceOrderDiscountId",
                table: "ApprovalRequests",
                column: "ServiceOrderDiscountId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_adjustments_approved_by",
                table: "attendance_adjustments",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_adjustments_staff_id",
                table: "attendance_adjustments",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_BranchId",
                table: "AuditLogs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TableName_RecordId",
                table: "AuditLogs",
                columns: new[] { "TableName", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_BayAllocations_AllocatedBy",
                table: "BayAllocations",
                column: "AllocatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BayAllocations_BayId_AllocatedAt",
                table: "BayAllocations",
                columns: new[] { "BayId", "AllocatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BayAllocations_ReleasedBy",
                table: "BayAllocations",
                column: "ReleasedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BayAllocations_ServiceOrderId_ReleasedAt",
                table: "BayAllocations",
                columns: new[] { "ServiceOrderId", "ReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BayAllocations_VehicleId",
                table: "BayAllocations",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Bays_BranchId",
                table: "Bays",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Bays_CreatedBy",
                table: "Bays",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Bays_CurrentOccupiedVehicleId",
                table: "Bays",
                column: "CurrentOccupiedVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Bays_DeletedBy",
                table: "Bays",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Bays_UpdatedBy",
                table: "Bays",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BayStatusLogs_BayId_ChangedAt",
                table: "BayStatusLogs",
                columns: new[] { "BayId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BayStatusLogs_ChangedBy",
                table: "BayStatusLogs",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_CreatedBy",
                table: "Branches",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_DeletedBy",
                table: "Branches",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_ManagerId",
                table: "Branches",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_UpdatedBy",
                table: "Branches",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BranchExceptions_ApprovedByStaffId",
                table: "BranchExceptions",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchExceptions_CreatedByStaffId",
                table: "BranchExceptions",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "UQ_BranchId_ExceptionDate",
                table: "BranchExceptions",
                columns: new[] { "BranchId", "ExceptionDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchOperatingHours_UpdatedByStaffId",
                table: "BranchOperatingHours",
                column: "UpdatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "UQ_BranchId_DayOfWeek",
                table: "BranchOperatingHours",
                columns: new[] { "BranchId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_BranchQuotationApprovalPolicies_BranchId",
                table: "BranchQuotationApprovalPolicies",
                column: "BranchId",
                unique: true);

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
                name: "IX_Categories_CreatedBy",
                table: "Categories",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DeletedBy",
                table: "Categories",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UpdatedBy",
                table: "Categories",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotActionLogs_CreatedAt",
                table: "ChatbotActionLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotActionLogs_SessionId",
                table: "ChatbotActionLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotActionLogs_SessionId_CreatedAt",
                table: "ChatbotActionLogs",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotMessages_CreatedAt",
                table: "ChatbotMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotMessages_SessionId",
                table: "ChatbotMessages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotMessages_SessionId_CreatedAt",
                table: "ChatbotMessages",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_BranchId",
                table: "ChatbotSessions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_ContextServiceCategoryId",
                table: "ChatbotSessions",
                column: "ContextServiceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_ContextVehicleId",
                table: "ChatbotSessions",
                column: "ContextVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_CreatedAt",
                table: "ChatbotSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_CustomerId",
                table: "ChatbotSessions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_CustomerId_Status_LastActivityAt",
                table: "ChatbotSessions",
                columns: new[] { "CustomerId", "Status", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_HandoffConversationId",
                table: "ChatbotSessions",
                column: "HandoffConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_LastActivityAt",
                table: "ChatbotSessions",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_Status",
                table: "ChatbotSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_ChatbotSessions_SessionToken",
                table: "ChatbotSessions",
                column: "SessionToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageAttachments_CreatedAt",
                table: "ChatMessageAttachments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageAttachments_MessageId_SortOrder_Id",
                table: "ChatMessageAttachments",
                columns: new[] { "MessageId", "SortOrder", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_Id",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_ChatMessages_ClientMessageId",
                table: "ChatMessages",
                column: "ClientMessageId",
                unique: true,
                filter: "[ClientMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_BranchId",
                table: "CommissionRules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_CreatedBy",
                table: "CommissionRules",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_DeletedBy",
                table: "CommissionRules",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_StaffTierId",
                table: "CommissionRules",
                column: "StaffTierId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_UpdatedBy",
                table: "CommissionRules",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTransactions_ApprovedBy",
                table: "CommissionTransactions",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTransactions_CreatedBy",
                table: "CommissionTransactions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTransactions_OriginalCommissionId",
                table: "CommissionTransactions",
                column: "OriginalCommissionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTransactions_RetailReturnId",
                table: "CommissionTransactions",
                column: "RetailReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTransactions_ServiceOrderId",
                table: "CommissionTransactions",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTransactions_ServiceStaffAssignmentId",
                table: "CommissionTransactions",
                column: "ServiceStaffAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTransactions_StaffId",
                table: "CommissionTransactions",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTransactions_UpdatedBy",
                table: "CommissionTransactions",
                column: "UpdatedBy");

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
                name: "IX_ConversationCustomerReads_CustomerId",
                table: "ConversationCustomerReads",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationCustomerReads_LastReadMessageId",
                table: "ConversationCustomerReads",
                column: "LastReadMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AppointmentId",
                table: "Conversations",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AssignedStaffId",
                table: "Conversations",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Branch_Status_LastMessageAt",
                table: "Conversations",
                columns: new[] { "BranchId", "Status", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Customer_LastMessageAt",
                table: "Conversations",
                columns: new[] { "CustomerId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_LastMessageId",
                table: "Conversations",
                column: "LastMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ServiceOrderId",
                table: "Conversations",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_StaffId",
                table: "Conversations",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_VehicleId",
                table: "Conversations",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationStaffReads_LastReadMessageId",
                table: "ConversationStaffReads",
                column: "LastReadMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationStaffReads_StaffId",
                table: "ConversationStaffReads",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedbacks_CustomerId",
                table: "CustomerFeedbacks",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedbacks_RetailOrderId",
                table: "CustomerFeedbacks",
                column: "RetailOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedbacks_ServiceOrderId",
                table: "CustomerFeedbacks",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CreatedBy",
                table: "Customers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_DeletedBy",
                table: "Customers",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TierId",
                table: "Customers",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UpdatedBy",
                table: "Customers",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTiers_CreatedBy",
                table: "CustomerTiers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTiers_DeletedBy",
                table: "CustomerTiers",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTiers_UpdatedBy",
                table: "CustomerTiers",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVehicles_CreatedBy",
                table: "CustomerVehicles",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVehicles_CustomerId",
                table: "CustomerVehicles",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVehicles_DeletedBy",
                table: "CustomerVehicles",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVehicles_UpdatedBy",
                table: "CustomerVehicles",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVehicles_VehicleId",
                table: "CustomerVehicles",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVouchers_CustomerId",
                table: "CustomerVouchers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVouchers_RetailOrderId",
                table: "CustomerVouchers",
                column: "RetailOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVouchers_ServiceOrderId",
                table: "CustomerVouchers",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVouchers_VoucherId",
                table: "CustomerVouchers",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_CreatedBy",
                table: "Deliveries",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_RetailOrderId",
                table: "Deliveries",
                column: "RetailOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_UpdatedBy",
                table: "Deliveries",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_ServiceOrderId",
                table: "Diagnoses",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_UpdatedBy",
                table: "Diagnoses",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosisAssignments_DiagnosisId",
                table: "DiagnosisAssignments",
                column: "DiagnosisId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosisRecommendations_CreatedBy",
                table: "DiagnosisRecommendations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosisRecommendations_DiagnosisAssignmentId",
                table: "DiagnosisRecommendations",
                column: "DiagnosisAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosisRecommendations_DiagnosisId",
                table: "DiagnosisRecommendations",
                column: "DiagnosisId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosisRecommendations_InventoryItemId",
                table: "DiagnosisRecommendations",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosisRecommendations_ParentRecommendationId",
                table: "DiagnosisRecommendations",
                column: "ParentRecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosisRecommendations_StandardServiceId",
                table: "DiagnosisRecommendations",
                column: "StandardServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosisRecommendations_UpdatedBy",
                table: "DiagnosisRecommendations",
                column: "UpdatedBy");

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
                name: "IX_ExpenseLogs_ApprovedBy",
                table: "ExpenseLogs",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLogs_BranchId",
                table: "ExpenseLogs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLogs_ShiftClosingId",
                table: "ExpenseLogs",
                column: "ShiftClosingId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLogs_StaffId",
                table: "ExpenseLogs",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePass_ApprovedBy",
                table: "GatePass",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GatePass_IssuedBy",
                table: "GatePass",
                column: "IssuedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GatePass_ScannedBy",
                table: "GatePass",
                column: "ScannedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GatePass_ServiceOrderId",
                table: "GatePass",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "UQ__GatePass__EE32FA44C549FC8E",
                table: "GatePass",
                column: "QrCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_GoodsReceiptNoteId",
                table: "GoodsReceiptNoteItems",
                column: "GoodsReceiptNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_ProductId",
                table: "GoodsReceiptNoteItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_PurchaseOrderItem_Product",
                table: "GoodsReceiptNoteItems",
                columns: new[] { "PurchaseOrderItemId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_GrnCode",
                table: "GoodsReceiptNotes",
                column: "GrnCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_PurchaseOrderId_ReceivedAt",
                table: "GoodsReceiptNotes",
                columns: new[] { "PurchaseOrderId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_ReceivedBy",
                table: "GoodsReceiptNotes",
                column: "ReceivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_WarehouseId",
                table: "GoodsReceiptNotes",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UX_GrnDailySnapshots_Branch_Date",
                table: "GrnDailySnapshots",
                columns: new[] { "BranchId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupSessions_BranchId",
                table: "GroupSessions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSessions_CreatedBy",
                table: "GroupSessions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSessions_CustomerId_CreatedAt",
                table: "GroupSessions",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupSessions_Status_CreatedAt",
                table: "GroupSessions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupSessions_UpdatedBy",
                table: "GroupSessions",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_GroupSessions_GroupCode",
                table: "GroupSessions",
                column: "GroupCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceCompanies_CreatedBy",
                table: "InsuranceCompanies",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceCompanies_DeletedBy",
                table: "InsuranceCompanies",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceCompanies_UpdatedBy",
                table: "InsuranceCompanies",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ProductId",
                table: "Inventories",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UQ_Warehouse_Product",
                table: "Inventories",
                columns: new[] { "WarehouseId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAuditDetails_AuditId",
                table: "InventoryAuditDetails",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAuditDetails_ProductId",
                table: "InventoryAuditDetails",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAudits_ApprovedBy",
                table: "InventoryAudits",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAudits_CreatedBy",
                table: "InventoryAudits",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAudits_UpdatedBy",
                table: "InventoryAudits",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAudits_WarehouseId",
                table: "InventoryAudits",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_InvoiceId",
                table: "InvoiceDetails",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_ProductId",
                table: "InvoiceDetails",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_StandardServiceId",
                table: "InvoiceDetails",
                column: "StandardServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreatedBy",
                table: "Invoices",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreditApprovedByStaffId",
                table: "Invoices",
                column: "CreditApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_RetailOrderId",
                table: "Invoices",
                column: "RetailOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ServiceOrderId",
                table: "Invoices",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UpdatedBy",
                table: "Invoices",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_VehicleId",
                table: "Invoices",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceReminders_Dedupe",
                table: "MaintenanceReminders",
                columns: new[] { "CustomerId", "VehicleId", "ReminderType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceReminders_ResultAppointmentId",
                table: "MaintenanceReminders",
                column: "ResultAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceReminders_Scheduled_Status",
                table: "MaintenanceReminders",
                columns: new[] { "ScheduledAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceReminders_ServiceOrderId",
                table: "MaintenanceReminders",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceReminders_VehicleId",
                table: "MaintenanceReminders",
                column: "VehicleId");

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
                name: "IX_MaterialRequestDetails_CreatedBy",
                table: "MaterialRequestDetails",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestDetails_ProductId",
                table: "MaterialRequestDetails",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestDetails_RequestId",
                table: "MaterialRequestDetails",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestDetails_ServiceOrderDetailId",
                table: "MaterialRequestDetails",
                column: "ServiceOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestDetails_UpdatedBy",
                table: "MaterialRequestDetails",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestDetails_WarehouseId",
                table: "MaterialRequestDetails",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_ServiceOrderId",
                table: "MaterialRequests",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_StorekeeperId",
                table: "MaterialRequests",
                column: "StorekeeperId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_TechnicianId",
                table: "MaterialRequests",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_CreatedBy",
                table: "MaterialReturns",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_MaterialRequestDetailId",
                table: "MaterialReturns",
                column: "MaterialRequestDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_ProductId",
                table: "MaterialReturns",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_ReceivedBy_StaffId",
                table: "MaterialReturns",
                column: "ReceivedBy_StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_ReturnedBy_StaffId",
                table: "MaterialReturns",
                column: "ReturnedBy_StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_ServiceOrderId",
                table: "MaterialReturns",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_UpdatedBy",
                table: "MaterialReturns",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ__Material__4CF726C9D2977954",
                table: "MaterialReturns",
                column: "ReturnCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelSpecificRules_ProductId",
                table: "ModelSpecificRules",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CustomerId",
                table: "Notifications",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_CreatedBy",
                table: "NotificationTemplates",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_DeletedBy",
                table: "NotificationTemplates",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_UpdatedBy",
                table: "NotificationTemplates",
                column: "UpdatedBy");

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
                name: "IX_PasswordResetTokens_CustomerId_ExpiresAt",
                table: "PasswordResetTokens",
                columns: new[] { "CustomerId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_StaffId_ExpiresAt",
                table: "PasswordResetTokens",
                columns: new[] { "StaffId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "UX_PasswordResetTokens_TokenHash",
                table: "PasswordResetTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_CreatedBy",
                table: "PaymentMethods",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_DeletedBy",
                table: "PaymentMethods",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_UpdatedBy",
                table: "PaymentMethods",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ__PaymentM__11E9210D55BD757B",
                table: "PaymentMethods",
                column: "MethodCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CashierId",
                table: "Payments",
                column: "CashierId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MethodId",
                table: "Payments",
                column: "MethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RetailOrderId",
                table: "Payments",
                column: "RetailOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReversedPaymentId",
                table: "Payments",
                column: "ReversedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ServiceOrderId",
                table: "Payments",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ShiftClosingId",
                table: "Payments",
                column: "ShiftClosingId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UpdatedBy",
                table: "Payments",
                column: "UpdatedBy");

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
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CreatedBy",
                table: "Products",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Products_DeletedBy",
                table: "Products",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Products_UpdatedBy",
                table: "Products",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ__Products__CA1ECF0DDDECDF2B",
                table: "Products",
                column: "SKU",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_ProductId",
                table: "PurchaseOrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId",
                table: "PurchaseOrderItems",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ApprovedByStaffId",
                table: "PurchaseOrders",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_BranchId",
                table: "PurchaseOrders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CreatedBy",
                table: "PurchaseOrders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_DestinationWarehouseId",
                table: "PurchaseOrders",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_QC_Inspections_QC_StaffId",
                table: "QC_Inspections",
                column: "QC_StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_QC_Inspections_ReworkTechnicianId",
                table: "QC_Inspections",
                column: "ReworkTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_QC_Inspections_ServiceOrderDetailId",
                table: "QC_Inspections",
                column: "ServiceOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_QC_Inspections_ServiceOrderId",
                table: "QC_Inspections",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QC_Inspections_TemplateId",
                table: "QC_Inspections",
                column: "TemplateId");

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
                name: "IX_QuotationApprovalActions_QuotationId_ApprovalType_CreatedAt",
                table: "QuotationApprovalActions",
                columns: new[] { "QuotationId", "ApprovalType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_ApprovedBy",
                table: "QuotationDetails",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_CanceledBy",
                table: "QuotationDetails",
                column: "CanceledBy");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_CreatedBy",
                table: "QuotationDetails",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_InsuranceCompanyId",
                table: "QuotationDetails",
                column: "InsuranceCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_QuotationId_ApprovalStatus",
                table: "QuotationDetails",
                columns: new[] { "QuotationId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_ServiceOrderId",
                table: "QuotationDetails",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_SourceQuotationDetailId",
                table: "QuotationDetails",
                column: "SourceQuotationDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDetails_UpdatedBy",
                table: "QuotationDetails",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDiscounts_AppliedBy",
                table: "QuotationDiscounts",
                column: "AppliedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDiscounts_CustomerTierId",
                table: "QuotationDiscounts",
                column: "CustomerTierId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDiscounts_QuotationId",
                table: "QuotationDiscounts",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDiscounts_VoucherId",
                table: "QuotationDiscounts",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "UQ_QuotationDiscounts_Quotation_DiscountType",
                table: "QuotationDiscounts",
                columns: new[] { "QuotationId", "DiscountType" },
                unique: true,
                filter: "[QuotationId] IS NOT NULL AND [DiscountType] IS NOT NULL");

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
                name: "IX_Quotations_DiagnosisAssignmentId",
                table: "Quotations",
                column: "DiagnosisAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_DiagnosisId",
                table: "Quotations",
                column: "DiagnosisId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_InsuranceChangeType",
                table: "Quotations",
                column: "InsuranceChangeType");

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
                name: "IX_RetailOrderItems_ProductId",
                table: "RetailOrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailOrderItems_RetailOrderId",
                table: "RetailOrderItems",
                column: "RetailOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailOrders_BranchId",
                table: "RetailOrders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailOrders_CreatedBy",
                table: "RetailOrders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RetailOrders_CustomerId",
                table: "RetailOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailOrders_UpdatedBy",
                table: "RetailOrders",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RetailOrders_VoucherId",
                table: "RetailOrders",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "UQ__RetailOr__999B5229199338C4",
                table: "RetailOrders",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetailReturnItems_CreatedBy",
                table: "RetailReturnItems",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RetailReturnItems_RetailOrderItemId",
                table: "RetailReturnItems",
                column: "RetailOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailReturnItems_UpdatedBy",
                table: "RetailReturnItems",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RetailReturnItems_WarehouseId",
                table: "RetailReturnItems",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UQ_RetailReturnItem_Ticket",
                table: "RetailReturnItems",
                columns: new[] { "RetailReturnId", "RetailOrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetailReturns_ApprovedBy",
                table: "RetailReturns",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RetailReturns_CreatedBy",
                table: "RetailReturns",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RetailReturns_ReceivedBy",
                table: "RetailReturns",
                column: "ReceivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RetailReturns_RetailOrderId",
                table: "RetailReturns",
                column: "RetailOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailReturns_UpdatedBy",
                table: "RetailReturns",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ__RetailRe__4CF726C98A4E23DB",
                table: "RetailReturns",
                column: "ReturnCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevenueSnapshots_BranchId",
                table: "RevenueSnapshots",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CreatedBy",
                table: "Roles",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_DeletedBy",
                table: "Roles",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_UpdatedBy",
                table: "Roles",
                column: "UpdatedBy");

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
                name: "IX_ServiceCategories_CreatedBy",
                table: "ServiceCategories",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategories_DeletedBy",
                table: "ServiceCategories",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategories_ParentCategoryId",
                table: "ServiceCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategories_UpdatedBy",
                table: "ServiceCategories",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ__ServiceC__A25C5AA76FAA12AA",
                table: "ServiceCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_AbsorbedApprovedBy",
                table: "ServiceOrderDetails",
                column: "AbsorbedApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_CreatedBy",
                table: "ServiceOrderDetails",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_InsuranceCompanyId",
                table: "ServiceOrderDetails",
                column: "InsuranceCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_OriginalOrderDetailId",
                table: "ServiceOrderDetails",
                column: "OriginalOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_QuotationDetailId",
                table: "ServiceOrderDetails",
                column: "QuotationDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_ServiceOrderId",
                table: "ServiceOrderDetails",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_SourceQuotationId",
                table: "ServiceOrderDetails",
                column: "SourceQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_StandardServiceId",
                table: "ServiceOrderDetails",
                column: "StandardServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_SupplementaryRequestedByStaffId",
                table: "ServiceOrderDetails",
                column: "SupplementaryRequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_SupplierId",
                table: "ServiceOrderDetails",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_TechnicianId",
                table: "ServiceOrderDetails",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDetails_UpdatedBy",
                table: "ServiceOrderDetails",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDiscounts_AppliedBy",
                table: "ServiceOrderDiscounts",
                column: "AppliedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDiscounts_CustomerTierId",
                table: "ServiceOrderDiscounts",
                column: "CustomerTierId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderDiscounts_VoucherId",
                table: "ServiceOrderDiscounts",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "UQ_ServiceOrder_DiscountCategory",
                table: "ServiceOrderDiscounts",
                columns: new[] { "ServiceOrderId", "DiscountCategory" },
                unique: true,
                filter: "[DiscountCategory] IS NOT NULL");

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
                name: "IX_ServiceOrders_AppointmentId",
                table: "ServiceOrders",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_CreatedBy",
                table: "ServiceOrders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_CustomerId",
                table: "ServiceOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_ExecuteBranchId",
                table: "ServiceOrders",
                column: "ExecuteBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_GroupSessionId",
                table: "ServiceOrders",
                column: "GroupSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_OriginalServiceOrderId",
                table: "ServiceOrders",
                column: "OriginalServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_OriginBranchId",
                table: "ServiceOrders",
                column: "OriginBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_QCInspectorId",
                table: "ServiceOrders",
                column: "QCInspectorId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_ReceptionistId",
                table: "ServiceOrders",
                column: "ReceptionistId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_ServiceAdvisorId",
                table: "ServiceOrders",
                column: "ServiceAdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_Status_CreatedAt",
                table: "ServiceOrders",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_UpdatedBy",
                table: "ServiceOrders",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_VehicleId",
                table: "ServiceOrders",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "UQ__ServiceO__A25C5AA7FBF72344",
                table: "ServiceOrders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderSnapshots_CreatedBy",
                table: "ServiceOrderSnapshots",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderSnapshots_ServiceOrderId",
                table: "ServiceOrderSnapshots",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRefunds_ApprovedBy",
                table: "ServiceRefunds",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRefunds_CreatedBy",
                table: "ServiceRefunds",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRefunds_ServiceOrderId",
                table: "ServiceRefunds",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRefunds_UpdatedBy",
                table: "ServiceRefunds",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStaffAssignments_AssignedBy",
                table: "ServiceStaffAssignments",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStaffAssignments_ServiceOrderDetailId",
                table: "ServiceStaffAssignments",
                column: "ServiceOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStaffAssignments_TechnicianId",
                table: "ServiceStaffAssignments",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStaffAssignments_UpdatedBy",
                table: "ServiceStaffAssignments",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftClosings_BranchId",
                table: "ShiftClosings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftClosings_CashierId",
                table: "ShiftClosings",
                column: "CashierId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftClosings_ManagerId",
                table: "ShiftClosings",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftClosings_WorkShiftId",
                table: "ShiftClosings",
                column: "WorkShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_BranchId",
                table: "Shifts",
                column: "BranchId");

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
                name: "IX_Staffs_BranchId",
                table: "Staffs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_CreatedBy",
                table: "Staffs",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_DeletedBy",
                table: "Staffs",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_RoleId",
                table: "Staffs",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_StaffTierId",
                table: "Staffs",
                column: "StaffTierId");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_TeamId",
                table: "Staffs",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_UpdatedBy",
                table: "Staffs",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StaffSchedules_ShiftId",
                table: "StaffSchedules",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffSchedules_StaffId",
                table: "StaffSchedules",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTiers_CreatedBy",
                table: "StaffTiers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTiers_DeletedBy",
                table: "StaffTiers",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTiers_UpdatedBy",
                table: "StaffTiers",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StandardServices_CategoryId",
                table: "StandardServices",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StandardServices_CreatedBy",
                table: "StandardServices",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StandardServices_DeletedBy",
                table: "StandardServices",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StandardServices_UpdatedBy",
                table: "StandardServices",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ__Standard__A25C5AA70956C7EB",
                table: "StandardServices",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_ResolvedByPurchaseOrderId",
                table: "StockAlerts",
                column: "ResolvedByPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_Unresolved",
                table: "StockAlerts",
                columns: new[] { "ProductId", "WarehouseId", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_WarehouseId",
                table: "StockAlerts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_CreatedBy",
                table: "StockTransactions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_MaterialReturnId",
                table: "StockTransactions",
                column: "MaterialReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_ProductId",
                table: "StockTransactions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_PurchaseOrderId",
                table: "StockTransactions",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_RetailOrderId",
                table: "StockTransactions",
                column: "RetailOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_RetailReturnId",
                table: "StockTransactions",
                column: "RetailReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CreatedBy",
                table: "Suppliers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_DeletedBy",
                table: "Suppliers",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_UpdatedBy",
                table: "Suppliers",
                column: "UpdatedBy");

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
                name: "UQ_TimesheetEntries_Staff_WorkDate_WorkType",
                table: "TimesheetEntries",
                columns: new[] { "StaffId", "WorkDate", "TaskType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleBrands_CreatedBy",
                table: "VehicleBrands",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleBrands_DeletedBy",
                table: "VehicleBrands",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleBrands_UpdatedBy",
                table: "VehicleBrands",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ__VehicleB__A25C5AA7E9C6A8B3",
                table: "VehicleBrands",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleConditionLogs_CreatedBy",
                table: "VehicleConditionLogs",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleConditionLogs_InspectorId",
                table: "VehicleConditionLogs",
                column: "InspectorId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleConditionLogs_ServiceOrderId",
                table: "VehicleConditionLogs",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleConditionLogs_UpdatedBy",
                table: "VehicleConditionLogs",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleInsurances_InsuranceCompanyId",
                table: "VehicleInsurances",
                column: "InsuranceCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleInsurances_VehicleId",
                table: "VehicleInsurances",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleModels_BrandId",
                table: "VehicleModels",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleModels_CreatedBy",
                table: "VehicleModels",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleModels_DeletedBy",
                table: "VehicleModels",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleModels_UpdatedBy",
                table: "VehicleModels",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CreatedBy",
                table: "Vehicles",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DeletedBy",
                table: "Vehicles",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ModelId",
                table: "Vehicles",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_UpdatedBy",
                table: "Vehicles",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Vehicles_LicensePlate",
                table: "Vehicles",
                column: "LicensePlate",
                unique: true,
                filter: "([LicensePlate] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ_Vehicles_VIN",
                table: "Vehicles",
                column: "VIN",
                unique: true,
                filter: "([VIN] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherApprovalRequests_ApproverId",
                table: "VoucherApprovalRequests",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherApprovalRequests_BranchId",
                table: "VoucherApprovalRequests",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherApprovalRequests_InvoiceId",
                table: "VoucherApprovalRequests",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherApprovalRequests_RedemptionId",
                table: "VoucherApprovalRequests",
                column: "RedemptionId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherApprovalRequests_RequestedBy",
                table: "VoucherApprovalRequests",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherApprovalRequests_RetailOrderId",
                table: "VoucherApprovalRequests",
                column: "RetailOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherApprovalRequests_ServiceOrderId",
                table: "VoucherApprovalRequests",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherApprovalRequests_VoucherId",
                table: "VoucherApprovalRequests",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherApprovalRequestVoucherRedemption_VoucherRedemptionsId",
                table: "VoucherApprovalRequestVoucherRedemption",
                column: "VoucherRedemptionsId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherAssignments_CreatedBy",
                table: "VoucherAssignments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherAssignments_UpdatedBy",
                table: "VoucherAssignments",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherAssignments_VoucherId",
                table: "VoucherAssignments",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherAuditLogs_BranchId",
                table: "VoucherAuditLogs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherAuditLogs_PerformedBy",
                table: "VoucherAuditLogs",
                column: "PerformedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherAuditLogs_VoucherId",
                table: "VoucherAuditLogs",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherCampaigns_CreatedBy",
                table: "VoucherCampaigns",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherCampaigns_DeletedBy",
                table: "VoucherCampaigns",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherCampaigns_OwnerStaffId",
                table: "VoucherCampaigns",
                column: "OwnerStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherCampaigns_UpdatedBy",
                table: "VoucherCampaigns",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherCodeBatches_CreatedBy",
                table: "VoucherCodeBatches",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherCodeBatches_VoucherId",
                table: "VoucherCodeBatches",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherConflictRules_CreatedBy",
                table: "VoucherConflictRules",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherConflictRules_UpdatedBy",
                table: "VoucherConflictRules",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherConflictRules_VoucherId",
                table: "VoucherConflictRules",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_AppliedBy",
                table: "VoucherRedemptions",
                column: "AppliedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_ApprovalRequestId",
                table: "VoucherRedemptions",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_ApprovedBy",
                table: "VoucherRedemptions",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_BranchId",
                table: "VoucherRedemptions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_CustomerId",
                table: "VoucherRedemptions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_InvoiceId",
                table: "VoucherRedemptions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_PaymentMethodId",
                table: "VoucherRedemptions",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_RetailOrderId",
                table: "VoucherRedemptions",
                column: "RetailOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_ServiceOrderId",
                table: "VoucherRedemptions",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_VehicleId",
                table: "VoucherRedemptions",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemptions_VoucherId",
                table: "VoucherRedemptions",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CampaignId",
                table: "Vouchers",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CreatedBy",
                table: "Vouchers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_DeletedBy",
                table: "Vouchers",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_UpdatedBy",
                table: "Vouchers",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ__Vouchers__A25C5AA7E33C4EC6",
                table: "Vouchers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsageRules_CreatedBy",
                table: "VoucherUsageRules",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsageRules_UpdatedBy",
                table: "VoucherUsageRules",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsageRules_VoucherId",
                table: "VoucherUsageRules",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_BranchId",
                table: "Warehouses",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_CreatedBy",
                table: "Warehouses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_DeletedBy",
                table: "Warehouses",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_ManagerId",
                table: "Warehouses",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_UpdatedBy",
                table: "Warehouses",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ__Warehous__A25C5AA73F13B012",
                table: "Warehouses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkSlots_AppointmentId",
                table: "WorkSlots",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSlots_BayId",
                table: "WorkSlots",
                column: "BayId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSlots_BranchId",
                table: "WorkSlots",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSlots_CustomerId",
                table: "WorkSlots",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSlots_ParentSlotId",
                table: "WorkSlots",
                column: "ParentSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSlots_ServiceOrderId",
                table: "WorkSlots",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSlots_TechnicianId",
                table: "WorkSlots",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSlots_VehicleId",
                table: "WorkSlots",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK__AI_Foreca__Vehic__1758727B",
                table: "AI_Forecasts",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__AI_SmartD__Branc__184C96B4",
                table: "AI_SmartDispatching",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentInsuranceAssets_Appointments",
                table: "AppointmentInsuranceAssets",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentInsuranceUploads_Appointments",
                table: "AppointmentInsuranceUploads",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id");

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
                name: "FK_Appointments_Staffs_UpdatedBy",
                table: "Appointments",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Appointme__Creat__1E05700A",
                table: "Appointments",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Appointme__Branc__1B29035F",
                table: "Appointments",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Appointme__Custo__1C1D2798",
                table: "Appointments",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Appointme__Vehic__1D114BD1",
                table: "Appointments",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ApprovalR__Appro__22CA2527",
                table: "ApprovalRequests",
                column: "ApproverId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ApprovalR__Reque__21D600EE",
                table: "ApprovalRequests",
                column: "RequestedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ApprovalR__GateP__1FEDB87C",
                table: "ApprovalRequests",
                column: "GatePassId",
                principalTable: "GatePass",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ApprovalR__Mater__20E1DCB5",
                table: "ApprovalRequests",
                column: "MaterialReturnId",
                principalTable: "MaterialReturns",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ApprovalR__Servi__1EF99443",
                table: "ApprovalRequests",
                column: "ServiceOrderDiscountId",
                principalTable: "ServiceOrderDiscounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_adjustments_Staffs_approved_by",
                table: "attendance_adjustments",
                column: "approved_by",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_adjustments_Staffs_staff_id",
                table: "attendance_adjustments",
                column: "staff_id",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK__AuditLogs__Branc__23BE4960",
                table: "AuditLogs",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BayAllocations_Bays_BayId",
                table: "BayAllocations",
                column: "BayId",
                principalTable: "Bays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BayAllocations_ServiceOrders_ServiceOrderId",
                table: "BayAllocations",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BayAllocations_Staffs_AllocatedBy",
                table: "BayAllocations",
                column: "AllocatedBy",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BayAllocations_Staffs_ReleasedBy",
                table: "BayAllocations",
                column: "ReleasedBy",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BayAllocations_Vehicles_VehicleId",
                table: "BayAllocations",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK__Bays__BranchId__24B26D99",
                table: "Bays",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Bays__CreatedBy__269AB60B",
                table: "Bays",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Bays__DeletedBy__2882FE7D",
                table: "Bays",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Bays__UpdatedBy__278EDA44",
                table: "Bays",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Bays__CurrentOcc__25A691D2",
                table: "Bays",
                column: "CurrentOccupiedVehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BayStatusLogs_Staffs_ChangedBy",
                table: "BayStatusLogs",
                column: "ChangedBy",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Staffs_CreatedBy",
                table: "Branches",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Staffs_DeletedBy",
                table: "Branches",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Staffs_ManagerId",
                table: "Branches",
                column: "ManagerId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Staffs_UpdatedBy",
                table: "Branches",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__BranchExc__Appro__2F2FFC0C",
                table: "BranchExceptions",
                column: "ApprovedByStaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__BranchExc__Creat__2E3BD7D3",
                table: "BranchExceptions",
                column: "CreatedByStaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__BranchOpe__Updat__3118447E",
                table: "BranchOperatingHours",
                column: "UpdatedByStaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CapacityConfigs_CreateBy",
                table: "CapacityConfigs",
                column: "CreateBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CapacityConfigs_UpdateBy",
                table: "CapacityConfigs",
                column: "UpdateBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Categorie__Creat__33008CF0",
                table: "Categories",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Categorie__Delet__34E8D562",
                table: "Categories",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Categorie__Updat__33F4B129",
                table: "Categories",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatbotActionLogs_ChatbotSessions_SessionId",
                table: "ChatbotActionLogs",
                column: "SessionId",
                principalTable: "ChatbotSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatbotMessages_ChatbotSessions_SessionId",
                table: "ChatbotMessages",
                column: "SessionId",
                principalTable: "ChatbotSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatbotSessions_Conversations_HandoffConversationId",
                table: "ChatbotSessions",
                column: "HandoffConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatbotSessions_Customers_CustomerId",
                table: "ChatbotSessions",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatbotSessions_ServiceCategories_ContextServiceCategoryId",
                table: "ChatbotSessions",
                column: "ContextServiceCategoryId",
                principalTable: "ServiceCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatbotSessions_Vehicles_ContextVehicleId",
                table: "ChatbotSessions",
                column: "ContextVehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessageAttachments_MessageId",
                table: "ChatMessageAttachments",
                column: "MessageId",
                principalTable: "ChatMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK__ChatMessa__Conve__35DCF99B",
                table: "ChatMessages",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Commissio__Creat__37C5420D",
                table: "CommissionRules",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Commissio__Delet__39AD8A7F",
                table: "CommissionRules",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Commissio__Updat__38B96646",
                table: "CommissionRules",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Commissio__Staff__36D11DD4",
                table: "CommissionRules",
                column: "StaffTierId",
                principalTable: "StaffTiers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CommissionTransactions_RetailReturnId",
                table: "CommissionTransactions",
                column: "RetailReturnId",
                principalTable: "RetailReturns",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CommissionTransactions_ServiceOrderId",
                table: "CommissionTransactions",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CommissionTransactions_ServiceStaffAssignmentId",
                table: "CommissionTransactions",
                column: "ServiceStaffAssignmentId",
                principalTable: "ServiceStaffAssignments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Commissio__Appro__3D7E1B63",
                table: "CommissionTransactions",
                column: "ApprovedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Commissio__Creat__3E723F9C",
                table: "CommissionTransactions",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Commissio__Staff__3B95D2F1",
                table: "CommissionTransactions",
                column: "StaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Commissio__Updat__3F6663D5",
                table: "CommissionTransactions",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsumableReturnBundleItems_ConsumableReturnBundles_BundleId",
                table: "ConsumableReturnBundleItems",
                column: "BundleId",
                principalTable: "ConsumableReturnBundles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsumableReturnBundleItems_MaterialRequestDetails_MaterialReqDetailId",
                table: "ConsumableReturnBundleItems",
                column: "MaterialReqDetailId",
                principalTable: "MaterialRequestDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsumableReturnBundleItems_Products_ProductId",
                table: "ConsumableReturnBundleItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsumableReturnBundles_ServiceOrders_ServiceOrderId",
                table: "ConsumableReturnBundles",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsumableReturnBundles_Staffs_ReceivedBy",
                table: "ConsumableReturnBundles",
                column: "ReceivedBy",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsumableReturnBundles_Staffs_TechnicianId",
                table: "ConsumableReturnBundles",
                column: "TechnicianId",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationCustomerReads_ConversationId",
                table: "ConversationCustomerReads",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationCustomerReads_CustomerId",
                table: "ConversationCustomerReads",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AssignedStaffId",
                table: "Conversations",
                column: "AssignedStaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Conversat__Staff__4336F4B9",
                table: "Conversations",
                column: "StaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_ServiceOrderId",
                table: "Conversations",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_VehicleId",
                table: "Conversations",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Conversat__Custo__442B18F2",
                table: "Conversations",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationStaffReads_StaffId",
                table: "ConversationStaffReads",
                column: "StaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerFeedbacks_RetailOrderId",
                table: "CustomerFeedbacks",
                column: "RetailOrderId",
                principalTable: "RetailOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerFeedbacks_ServiceOrderId",
                table: "CustomerFeedbacks",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerF__Custo__451F3D2B",
                table: "CustomerFeedbacks",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Customers__Creat__48EFCE0F",
                table: "Customers",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Customers__Delet__4AD81681",
                table: "Customers",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Customers__Updat__49E3F248",
                table: "Customers",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Customers__TierI__47FBA9D6",
                table: "Customers",
                column: "TierId",
                principalTable: "CustomerTiers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerT__Creat__4BCC3ABA",
                table: "CustomerTiers",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerT__Delet__4DB4832C",
                table: "CustomerTiers",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerT__Updat__4CC05EF3",
                table: "CustomerTiers",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerV__Creat__5090EFD7",
                table: "CustomerVehicles",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerV__Delet__52793849",
                table: "CustomerVehicles",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerV__Updat__51851410",
                table: "CustomerVehicles",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerV__Vehic__4F9CCB9E",
                table: "CustomerVehicles",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerV__Retai__5649C92D",
                table: "CustomerVouchers",
                column: "RetailOrderId",
                principalTable: "RetailOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerV__Servi__5555A4F4",
                table: "CustomerVouchers",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__CustomerV__Vouch__546180BB",
                table: "CustomerVouchers",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Deliverie__Creat__5832119F",
                table: "Deliveries",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Deliverie__Updat__592635D8",
                table: "Deliveries",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Deliverie__Retai__573DED66",
                table: "Deliveries",
                column: "RetailOrderId",
                principalTable: "RetailOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Diagnoses__Servi__5FD33367",
                table: "Diagnoses",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Diagnoses__Updat__62AFA012",
                table: "Diagnoses",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__DiagnoseR__Creat__5DEAEAF5",
                table: "DiagnosisRecommendations",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__DiagnoseR__Updat__5EDF0F2E",
                table: "DiagnosisRecommendations",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__DiagnoseR__Inven__5CF6C6BC",
                table: "DiagnosisRecommendations",
                column: "InventoryItemId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__DiagnoseR__Stand__5C02A283",
                table: "DiagnosisRecommendations",
                column: "StandardServiceId",
                principalTable: "StandardServices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_employee_dependents_Staffs_staff_id",
                table: "employee_dependents",
                column: "staff_id",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_loans_Staffs_staff_id",
                table: "employee_loans",
                column: "staff_id",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK__ExpenseLo__Appro__668030F6",
                table: "ExpenseLogs",
                column: "ApprovedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ExpenseLo__Staff__658C0CBD",
                table: "ExpenseLogs",
                column: "StaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ExpenseLo__Shift__6497E884",
                table: "ExpenseLogs",
                column: "ShiftClosingId",
                principalTable: "ShiftClosings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__GatePass__Approv__695C9DA1",
                table: "GatePass",
                column: "ApprovedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__GatePass__Issued__68687968",
                table: "GatePass",
                column: "IssuedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__GatePass__Scanne__6A50C1DA",
                table: "GatePass",
                column: "ScannedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__GatePass__Servic__6774552F",
                table: "GatePass",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptNoteItems_GoodsReceiptNotes_GoodsReceiptNoteId",
                table: "GoodsReceiptNoteItems",
                column: "GoodsReceiptNoteId",
                principalTable: "GoodsReceiptNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptNoteItems_Products_ProductId",
                table: "GoodsReceiptNoteItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptNoteItems_PurchaseOrderItems_PurchaseOrderItemId",
                table: "GoodsReceiptNoteItems",
                column: "PurchaseOrderItemId",
                principalTable: "PurchaseOrderItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptNotes_PurchaseOrders_PurchaseOrderId",
                table: "GoodsReceiptNotes",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptNotes_Staffs_ReceivedBy",
                table: "GoodsReceiptNotes",
                column: "ReceivedBy",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptNotes_Warehouses_WarehouseId",
                table: "GoodsReceiptNotes",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSessions_CreatedBy",
                table: "GroupSessions",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSessions_UpdatedBy",
                table: "GroupSessions",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Insurance__Creat__6B44E613",
                table: "InsuranceCompanies",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Insurance__Delet__6D2D2E85",
                table: "InsuranceCompanies",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Insurance__Updat__6C390A4C",
                table: "InsuranceCompanies",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Inventori__Produ__6F1576F7",
                table: "Inventories",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Inventori__Wareh__6E2152BE",
                table: "Inventories",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Inventory__Audit__70099B30",
                table: "InventoryAuditDetails",
                column: "AuditId",
                principalTable: "InventoryAudits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Inventory__Produ__70FDBF69",
                table: "InventoryAuditDetails",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Inventory__Appro__72E607DB",
                table: "InventoryAudits",
                column: "ApprovedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Inventory__Creat__73DA2C14",
                table: "InventoryAudits",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Inventory__Updat__74CE504D",
                table: "InventoryAudits",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Inventory__Wareh__71F1E3A2",
                table: "InventoryAudits",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__InvoiceDe__Invoi__75C27486",
                table: "InvoiceDetails",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__InvoiceDe__Produ__76B698BF",
                table: "InvoiceDetails",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__InvoiceDe__Stand__77AABCF8",
                table: "InvoiceDetails",
                column: "StandardServiceId",
                principalTable: "StandardServices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Invoices__Create__7C6F7215",
                table: "Invoices",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Invoices__Credit__7B7B4DDC",
                table: "Invoices",
                column: "CreditApprovedByStaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Invoices__Update__7D63964E",
                table: "Invoices",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Invoices__Retail__7993056A",
                table: "Invoices",
                column: "RetailOrderId",
                principalTable: "RetailOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Invoices__Servic__789EE131",
                table: "Invoices",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Invoices__Vehicl__7A8729A3",
                table: "Invoices",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceReminders_ServiceOrders_ServiceOrderId",
                table: "MaintenanceReminders",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceReminders_Vehicles_VehicleId",
                table: "MaintenanceReminders",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MandatoryOverrideRequests_QuotationDetails_QuotationDetailId",
                table: "MandatoryOverrideRequests",
                column: "QuotationDetailId",
                principalTable: "QuotationDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MandatoryOverrideRequests_Quotations_QuotationId",
                table: "MandatoryOverrideRequests",
                column: "QuotationId",
                principalTable: "Quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MandatoryOverrideRequests_ServiceOrders_ServiceOrderId",
                table: "MandatoryOverrideRequests",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequestDetails_MaterialRequests_RequestId",
                table: "MaterialRequestDetails",
                column: "RequestId",
                principalTable: "MaterialRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequestDetails_ServiceOrderDetailId",
                table: "MaterialRequestDetails",
                column: "ServiceOrderDetailId",
                principalTable: "ServiceOrderDetails",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Creat__004002F9",
                table: "MaterialRequestDetails",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Updat__01342732",
                table: "MaterialRequestDetails",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Produ__7E57BA87",
                table: "MaterialRequestDetails",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Wareh__7F4BDEC0",
                table: "MaterialRequestDetails",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequests_ServiceOrderId",
                table: "MaterialRequests",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Store__041093DD",
                table: "MaterialRequests",
                column: "StorekeeperId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Techn__031C6FA4",
                table: "MaterialRequests",
                column: "TechnicianId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialReturns_ServiceOrderId",
                table: "MaterialReturns",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Creat__09C96D33",
                table: "MaterialReturns",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Recei__08D548FA",
                table: "MaterialReturns",
                column: "ReceivedBy_StaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Retur__07E124C1",
                table: "MaterialReturns",
                column: "ReturnedBy_StaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Updat__0ABD916C",
                table: "MaterialReturns",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__MaterialR__Produ__06ED0088",
                table: "MaterialReturns",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ModelSpec__Model__0CA5D9DE",
                table: "ModelSpecificRules",
                column: "ModelId",
                principalTable: "VehicleModels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ModelSpec__Produ__0D99FE17",
                table: "ModelSpecificRules",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Notificat__Creat__0F824689",
                table: "NotificationTemplates",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Notificat__Delet__116A8EFB",
                table: "NotificationTemplates",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Notificat__Updat__10766AC2",
                table: "NotificationTemplates",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_overtime_entries_Staffs_approved_by",
                table: "overtime_entries",
                column: "approved_by",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_overtime_entries_Staffs_staff_id",
                table: "overtime_entries",
                column: "staff_id",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PackageItems_Packages",
                table: "PackageItems",
                column: "PackageId",
                principalTable: "Packages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PackageItems_StandardServices",
                table: "PackageItems",
                column: "StandardServiceId",
                principalTable: "StandardServices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_ServiceCategories",
                table: "Packages",
                column: "CategoryId",
                principalTable: "ServiceCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PartInventoryReservations_CreateBy",
                table: "PartInventoryReservations",
                column: "CreateBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PartInventoryReservations_UpdateBy",
                table: "PartInventoryReservations",
                column: "UpdateBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PartInventoryReservations_Product",
                table: "PartInventoryReservations",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PartInventoryReservations_WorkSlot",
                table: "PartInventoryReservations",
                column: "WorkSlotId",
                principalTable: "WorkSlots",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PasswordResetTokens_Staffs_StaffId",
                table: "PasswordResetTokens",
                column: "StaffId",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK__PaymentMe__Creat__125EB334",
                table: "PaymentMethods",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__PaymentMe__Delet__1446FBA6",
                table: "PaymentMethods",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__PaymentMe__Updat__1352D76D",
                table: "PaymentMethods",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Payments__Cashie__162F4418",
                table: "Payments",
                column: "CashierId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Payments__Update__1BE81D6E",
                table: "Payments",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Payments__Retail__190BB0C3",
                table: "Payments",
                column: "RetailOrderId",
                principalTable: "RetailOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Payments__Servic__17236851",
                table: "Payments",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Payments__ShiftC__153B1FDF",
                table: "Payments",
                column: "ShiftClosingId",
                principalTable: "ShiftClosings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_payroll_items_Staffs_staff_id",
                table: "payroll_items",
                column: "staff_id",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payroll_items_payroll_runs_payroll_run_id",
                table: "payroll_items",
                column: "payroll_run_id",
                principalTable: "payroll_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payroll_runs_Staffs_approved_by",
                table: "payroll_runs",
                column: "approved_by",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_payroll_runs_Staffs_calculated_by",
                table: "payroll_runs",
                column: "calculated_by",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_payslips_Staffs_issued_by",
                table: "payslips",
                column: "issued_by",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_pit_profiles_Staffs_staff_id",
                table: "pit_profiles",
                column: "staff_id",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK__ProductIm__Produ__1CDC41A7",
                table: "ProductImages",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Products__Create__1EC48A19",
                table: "Products",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Products__Delete__20ACD28B",
                table: "Products",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Products__Update__1FB8AE52",
                table: "Products",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__PurchaseO__Purch__21A0F6C4",
                table: "PurchaseOrderItems",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__PurchaseO__Appro__2665ABE1",
                table: "PurchaseOrders",
                column: "ApprovedByStaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__PurchaseO__Creat__2759D01A",
                table: "PurchaseOrders",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__PurchaseO__Desti__247D636F",
                table: "PurchaseOrders",
                column: "DestinationWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__PurchaseO__Suppl__257187A8",
                table: "PurchaseOrders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__QC_Inspec__QC_St__2942188C",
                table: "QC_Inspections",
                column: "QC_StaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__QC_Inspec__Rewor__2B2A60FE",
                table: "QC_Inspections",
                column: "ReworkTechnicianId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__QC_Inspec__Servi__284DF453",
                table: "QC_Inspections",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__QC_Inspec__Servi__2A363CC5",
                table: "QC_Inspections",
                column: "ServiceOrderDetailId",
                principalTable: "ServiceOrderDetails",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QcChecklistItems_Staffs_CheckedBy",
                table: "QcChecklistItems",
                column: "CheckedBy",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationAccessLinks_Quotations",
                table: "QuotationAccessLinks",
                column: "QuotationId",
                principalTable: "Quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationApprovalActions_Quotations",
                table: "QuotationApprovalActions",
                column: "QuotationId",
                principalTable: "Quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDetails_ApprovedBy",
                table: "QuotationDetails",
                column: "ApprovedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDetails_CanceledBy",
                table: "QuotationDetails",
                column: "CanceledBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDetails_CreatedBy",
                table: "QuotationDetails",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDetails_UpdatedBy",
                table: "QuotationDetails",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDetails_Quotations",
                table: "QuotationDetails",
                column: "QuotationId",
                principalTable: "Quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDetails_ServiceOrders",
                table: "QuotationDetails",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDiscounts_AppliedBy",
                table: "QuotationDiscounts",
                column: "AppliedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDiscounts_QuotationId",
                table: "QuotationDiscounts",
                column: "QuotationId",
                principalTable: "Quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDiscounts_VoucherId",
                table: "QuotationDiscounts",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_AppliedBy",
                table: "Quotations",
                column: "AppliedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_ApprovedBy",
                table: "Quotations",
                column: "ApprovedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_CreatedBy",
                table: "Quotations",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_ServiceAdvisor",
                table: "Quotations",
                column: "ServiceAdvisorId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_UpdatedBy",
                table: "Quotations",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_ServiceOrders",
                table: "Quotations",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_TechnicalProposalSnapshots_SourceSnapshotId",
                table: "Quotations",
                column: "SourceSnapshotId",
                principalTable: "TechnicalProposalSnapshots",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailOrd__Retai__2C1E8537",
                table: "RetailOrderItems",
                column: "RetailOrderId",
                principalTable: "RetailOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RetailOrders_VoucherId",
                table: "RetailOrders",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailOrd__Creat__2FEF161B",
                table: "RetailOrders",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailOrd__Updat__30E33A54",
                table: "RetailOrders",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailRet__Creat__35A7EF71",
                table: "RetailReturnItems",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailRet__Updat__369C13AA",
                table: "RetailReturnItems",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailRet__Retai__32CB82C6",
                table: "RetailReturnItems",
                column: "RetailReturnId",
                principalTable: "RetailReturns",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailRet__Wareh__34B3CB38",
                table: "RetailReturnItems",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailRet__Appro__39788055",
                table: "RetailReturns",
                column: "ApprovedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailRet__Creat__3A6CA48E",
                table: "RetailReturns",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailRet__Recei__38845C1C",
                table: "RetailReturns",
                column: "ReceivedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__RetailRet__Updat__3B60C8C7",
                table: "RetailReturns",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Staffs_CreatedBy",
                table: "Roles",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Staffs_DeletedBy",
                table: "Roles",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Staffs_UpdatedBy",
                table: "Roles",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_salary_advances_Staffs_approved_by",
                table: "salary_advances",
                column: "approved_by",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_salary_advances_Staffs_staff_id",
                table: "salary_advances",
                column: "staff_id",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceBOMs_CreateBy",
                table: "ServiceBOMs",
                column: "CreateBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceBOMs_UpdateBy",
                table: "ServiceBOMs",
                column: "UpdateBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceBOMs_Service",
                table: "ServiceBOMs",
                column: "ServiceId",
                principalTable: "StandardServices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceBOMs_VehicleModel",
                table: "ServiceBOMs",
                column: "VehicleModelId",
                principalTable: "VehicleModels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceCa__Creat__40257DE4",
                table: "ServiceCategories",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceCa__Delet__420DC656",
                table: "ServiceCategories",
                column: "DeletedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceCa__Updat__4119A21D",
                table: "ServiceCategories",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrderDetails_SupplementaryRequestedByStaff",
                table: "ServiceOrderDetails",
                column: "SupplementaryRequestedByStaffId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Absor__43F60EC8",
                table: "ServiceOrderDetails",
                column: "AbsorbedApprovedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Creat__44EA3301",
                table: "ServiceOrderDetails",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Techn__49AEE81E",
                table: "ServiceOrderDetails",
                column: "TechnicianId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Updat__45DE573A",
                table: "ServiceOrderDetails",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Servi__46D27B73",
                table: "ServiceOrderDetails",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Stand__48BAC3E5",
                table: "ServiceOrderDetails",
                column: "StandardServiceId",
                principalTable: "StandardServices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Suppl__4B973090",
                table: "ServiceOrderDetails",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrderDiscounts_VoucherId",
                table: "ServiceOrderDiscounts",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Appli__4F67C174",
                table: "ServiceOrderDiscounts",
                column: "AppliedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Servi__4D7F7902",
                table: "ServiceOrderDiscounts",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrderInsuranceAssets_ServiceOrders",
                table: "ServiceOrderInsuranceAssets",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrderInsuranceUploads_ServiceOrders",
                table: "ServiceOrderInsuranceUploads",
                column: "ServiceOrderId",
                principalTable: "ServiceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Creat__59E54FE7",
                table: "ServiceOrders",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__QCIns__58F12BAE",
                table: "ServiceOrders",
                column: "QCInspectorId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Recep__57FD0775",
                table: "ServiceOrders",
                column: "ReceptionistId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Servi__5708E33C",
                table: "ServiceOrders",
                column: "ServiceAdvisorId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Updat__5AD97420",
                table: "ServiceOrders",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Vehic__52442E1F",
                table: "ServiceOrders",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceOr__Creat__5CC1BC92",
                table: "ServiceOrderSnapshots",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceRe__Appro__5EAA0504",
                table: "ServiceRefunds",
                column: "ApprovedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceRe__Creat__5F9E293D",
                table: "ServiceRefunds",
                column: "CreatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceRe__Updat__60924D76",
                table: "ServiceRefunds",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceSt__Assig__627A95E8",
                table: "ServiceStaffAssignments",
                column: "AssignedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceSt__Techn__636EBA21",
                table: "ServiceStaffAssignments",
                column: "TechnicianId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ServiceSt__Updat__6462DE5A",
                table: "ServiceStaffAssignments",
                column: "UpdatedBy",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ShiftClos__Cashi__673F4B05",
                table: "ShiftClosings",
                column: "CashierId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ShiftClos__Manag__68336F3E",
                table: "ShiftClosings",
                column: "ManagerId",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_staff_contracts_Staffs_staff_id",
                table: "staff_contracts",
                column: "staff_id",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_staff_salary_profiles_Staffs_staff_id",
                table: "staff_salary_profiles",
                column: "staff_id",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_staff_salary_profiles_Staffs_updated_by",
                table: "staff_salary_profiles",
                column: "updated_by",
                principalTable: "Staffs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Staffs_TeamId",
                table: "Staffs",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Staffs__StaffTie__6C040022",
                table: "Staffs",
                column: "StaffTierId",
                principalTable: "StaffTiers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VoucherApprovalRequests_VoucherRedemptions_RedemptionId",
                table: "VoucherApprovalRequests",
                column: "RedemptionId",
                principalTable: "VoucherRedemptions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__Appointme__Vehic__1D114BD1",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_VehicleId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK__Invoices__Vehicl__7A8729A3",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__Vehic__52442E1F",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherRedemptions_Vehicles_VehicleId",
                table: "VoucherRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK__Appointme__Branc__1B29035F",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_BranchId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupSessions_BranchId",
                table: "GroupSessions");

            migrationBuilder.DropForeignKey(
                name: "FK__RetailOrd__Branc__2E06CDA9",
                table: "RetailOrders");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__Execu__55209ACA",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__Origi__542C7691",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK__Staffs__BranchId__6B0FDBE9",
                table: "Staffs");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_BranchId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherApprovalRequests_Branches_BranchId",
                table: "VoucherApprovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherRedemptions_Branches_BranchId",
                table: "VoucherRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AppointmentId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__Appoi__53385258",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrders_GroupSessionId",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AssignedStaffId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK__Conversat__Staff__4336F4B9",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK__Customers__Creat__48EFCE0F",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK__Customers__Delet__4AD81681",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK__Customers__Updat__49E3F248",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK__CustomerT__Creat__4BCC3ABA",
                table: "CustomerTiers");

            migrationBuilder.DropForeignKey(
                name: "FK__CustomerT__Delet__4DB4832C",
                table: "CustomerTiers");

            migrationBuilder.DropForeignKey(
                name: "FK__CustomerT__Updat__4CC05EF3",
                table: "CustomerTiers");

            migrationBuilder.DropForeignKey(
                name: "FK__Invoices__Create__7C6F7215",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK__Invoices__Credit__7B7B4DDC",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK__Invoices__Update__7D63964E",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK__PaymentMe__Creat__125EB334",
                table: "PaymentMethods");

            migrationBuilder.DropForeignKey(
                name: "FK__PaymentMe__Delet__1446FBA6",
                table: "PaymentMethods");

            migrationBuilder.DropForeignKey(
                name: "FK__PaymentMe__Updat__1352D76D",
                table: "PaymentMethods");

            migrationBuilder.DropForeignKey(
                name: "FK__RetailOrd__Creat__2FEF161B",
                table: "RetailOrders");

            migrationBuilder.DropForeignKey(
                name: "FK__RetailOrd__Updat__30E33A54",
                table: "RetailOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Staffs_CreatedBy",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Staffs_DeletedBy",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Staffs_UpdatedBy",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__Creat__59E54FE7",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__QCIns__58F12BAE",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__Recep__57FD0775",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__Servi__5708E33C",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__Updat__5AD97420",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffTiers_Staffs_CreatedBy",
                table: "StaffTiers");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffTiers_Staffs_DeletedBy",
                table: "StaffTiers");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffTiers_Staffs_UpdatedBy",
                table: "StaffTiers");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_LeaderId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherApprovalRequests_Staffs_ApproverId",
                table: "VoucherApprovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherApprovalRequests_Staffs_RequestedBy",
                table: "VoucherApprovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherCampaigns_Staffs_CreatedBy",
                table: "VoucherCampaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherCampaigns_Staffs_DeletedBy",
                table: "VoucherCampaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherCampaigns_Staffs_OwnerStaffId",
                table: "VoucherCampaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherCampaigns_Staffs_UpdatedBy",
                table: "VoucherCampaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherRedemptions_Staffs_AppliedBy",
                table: "VoucherRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherRedemptions_Staffs_ApprovedBy",
                table: "VoucherRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK__Vouchers__Create__10416098",
                table: "Vouchers");

            migrationBuilder.DropForeignKey(
                name: "FK__Vouchers__Delete__1229A90A",
                table: "Vouchers");

            migrationBuilder.DropForeignKey(
                name: "FK__Vouchers__Update__113584D1",
                table: "Vouchers");

            migrationBuilder.DropForeignKey(
                name: "FK__Conversat__Custo__442B18F2",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK__RetailOrd__Custo__2EFAF1E2",
                table: "RetailOrders");

            migrationBuilder.DropForeignKey(
                name: "FK__ServiceOr__Custo__515009E6",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherRedemptions_Customers_CustomerId",
                table: "VoucherRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_ServiceOrderId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK__Invoices__Servic__789EE131",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherApprovalRequests_ServiceOrders_ServiceOrderId",
                table: "VoucherApprovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherRedemptions_ServiceOrders_ServiceOrderId",
                table: "VoucherRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK__ChatMessa__Conve__35DCF99B",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK__Invoices__Retail__7993056A",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherApprovalRequests_RetailOrders_RetailOrderId",
                table: "VoucherApprovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherRedemptions_RetailOrders_RetailOrderId",
                table: "VoucherRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherApprovalRequests_Vouchers_VoucherId",
                table: "VoucherApprovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherRedemptions_Vouchers_VoucherId",
                table: "VoucherRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherApprovalRequests_Invoices_InvoiceId",
                table: "VoucherApprovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherRedemptions_Invoices_InvoiceId",
                table: "VoucherRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherRedemptions_PaymentMethods_PaymentMethodId",
                table: "VoucherRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherApprovalRequests_VoucherRedemptions_RedemptionId",
                table: "VoucherApprovalRequests");

            migrationBuilder.DropTable(
                name: "AI_Forecasts");

            migrationBuilder.DropTable(
                name: "AI_FraudMonitors");

            migrationBuilder.DropTable(
                name: "AI_KnowledgeBase");

            migrationBuilder.DropTable(
                name: "AI_ModelConfigs");

            migrationBuilder.DropTable(
                name: "AI_SmartDispatching");

            migrationBuilder.DropTable(
                name: "AppointmentInsuranceAssets");

            migrationBuilder.DropTable(
                name: "AppointmentInsuranceUploads");

            migrationBuilder.DropTable(
                name: "ApprovalRequests");

            migrationBuilder.DropTable(
                name: "attendance_adjustments");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BayAllocations");

            migrationBuilder.DropTable(
                name: "BayStatusLogs");

            migrationBuilder.DropTable(
                name: "BranchExceptions");

            migrationBuilder.DropTable(
                name: "BranchOperatingHours");

            migrationBuilder.DropTable(
                name: "BranchQuotationApprovalPolicies");

            migrationBuilder.DropTable(
                name: "CapacityConfigs");

            migrationBuilder.DropTable(
                name: "ChatbotActionLogs");

            migrationBuilder.DropTable(
                name: "ChatbotMessages");

            migrationBuilder.DropTable(
                name: "ChatMessageAttachments");

            migrationBuilder.DropTable(
                name: "CommissionRules");

            migrationBuilder.DropTable(
                name: "CommissionTransactions");

            migrationBuilder.DropTable(
                name: "ConsumableReturnBundleItems");

            migrationBuilder.DropTable(
                name: "ConversationCustomerReads");

            migrationBuilder.DropTable(
                name: "ConversationStaffReads");

            migrationBuilder.DropTable(
                name: "CustomerFeedbacks");

            migrationBuilder.DropTable(
                name: "CustomerVehicles");

            migrationBuilder.DropTable(
                name: "CustomerVouchers");

            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "DiagnosisRecommendations");

            migrationBuilder.DropTable(
                name: "employee_dependents");

            migrationBuilder.DropTable(
                name: "employee_loans");

            migrationBuilder.DropTable(
                name: "ExpenseLogs");

            migrationBuilder.DropTable(
                name: "GoodsReceiptNoteItems");

            migrationBuilder.DropTable(
                name: "GrnDailySnapshots");

            migrationBuilder.DropTable(
                name: "Inventories");

            migrationBuilder.DropTable(
                name: "InventoryAuditDetails");

            migrationBuilder.DropTable(
                name: "InvoiceDetails");

            migrationBuilder.DropTable(
                name: "MaintenanceReminders");

            migrationBuilder.DropTable(
                name: "MandatoryOverrideRequests");

            migrationBuilder.DropTable(
                name: "ModelSpecificRules");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "overtime_entries");

            migrationBuilder.DropTable(
                name: "PackageItems");

            migrationBuilder.DropTable(
                name: "PartInventoryReservations");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "payslips");

            migrationBuilder.DropTable(
                name: "pit_profiles");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "QcChecklistItems");

            migrationBuilder.DropTable(
                name: "QuotationAccessLinks");

            migrationBuilder.DropTable(
                name: "QuotationApprovalActions");

            migrationBuilder.DropTable(
                name: "QuotationDiscounts");

            migrationBuilder.DropTable(
                name: "RetailReturnItems");

            migrationBuilder.DropTable(
                name: "RevenueSnapshots");

            migrationBuilder.DropTable(
                name: "salary_advances");

            migrationBuilder.DropTable(
                name: "SalaryCoefficients");

            migrationBuilder.DropTable(
                name: "ServiceBOMs");

            migrationBuilder.DropTable(
                name: "ServiceOrderInsuranceAssets");

            migrationBuilder.DropTable(
                name: "ServiceOrderInsuranceUploads");

            migrationBuilder.DropTable(
                name: "ServiceOrderSnapshots");

            migrationBuilder.DropTable(
                name: "ServiceRefunds");

            migrationBuilder.DropTable(
                name: "staff_contracts");

            migrationBuilder.DropTable(
                name: "staff_salary_profiles");

            migrationBuilder.DropTable(
                name: "StaffSchedules");

            migrationBuilder.DropTable(
                name: "StockAlerts");

            migrationBuilder.DropTable(
                name: "StockTransactions");

            migrationBuilder.DropTable(
                name: "TechnicalProposalSnapshotDetails");

            migrationBuilder.DropTable(
                name: "TimesheetEntries");

            migrationBuilder.DropTable(
                name: "VehicleConditionLogs");

            migrationBuilder.DropTable(
                name: "VehicleInsurances");

            migrationBuilder.DropTable(
                name: "VoucherApprovalRequestVoucherRedemption");

            migrationBuilder.DropTable(
                name: "VoucherAssignments");

            migrationBuilder.DropTable(
                name: "VoucherAuditLogs");

            migrationBuilder.DropTable(
                name: "VoucherCodeBatches");

            migrationBuilder.DropTable(
                name: "VoucherConflictRules");

            migrationBuilder.DropTable(
                name: "VoucherUsageRules");

            migrationBuilder.DropTable(
                name: "GatePass");

            migrationBuilder.DropTable(
                name: "ServiceOrderDiscounts");

            migrationBuilder.DropTable(
                name: "ChatbotSessions");

            migrationBuilder.DropTable(
                name: "ServiceStaffAssignments");

            migrationBuilder.DropTable(
                name: "ConsumableReturnBundles");

            migrationBuilder.DropTable(
                name: "GoodsReceiptNotes");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItems");

            migrationBuilder.DropTable(
                name: "InventoryAudits");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "WorkSlots");

            migrationBuilder.DropTable(
                name: "ShiftClosings");

            migrationBuilder.DropTable(
                name: "payroll_items");

            migrationBuilder.DropTable(
                name: "QC_Inspections");

            migrationBuilder.DropTable(
                name: "RetailOrderItems");

            migrationBuilder.DropTable(
                name: "MaterialReturns");

            migrationBuilder.DropTable(
                name: "RetailReturns");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "Bays");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "payroll_runs");

            migrationBuilder.DropTable(
                name: "QcChecklistTemplates");

            migrationBuilder.DropTable(
                name: "MaterialRequestDetails");

            migrationBuilder.DropTable(
                name: "payroll_periods");

            migrationBuilder.DropTable(
                name: "MaterialRequests");

            migrationBuilder.DropTable(
                name: "ServiceOrderDetails");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropTable(
                name: "QuotationDetails");

            migrationBuilder.DropTable(
                name: "StandardServices");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Quotations");

            migrationBuilder.DropTable(
                name: "ServiceCategories");

            migrationBuilder.DropTable(
                name: "DiagnosisAssignments");

            migrationBuilder.DropTable(
                name: "TechnicalProposalSnapshots");

            migrationBuilder.DropTable(
                name: "Diagnoses");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "VehicleModels");

            migrationBuilder.DropTable(
                name: "VehicleBrands");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "InsuranceCompanies");

            migrationBuilder.DropTable(
                name: "GroupSessions");

            migrationBuilder.DropTable(
                name: "Staffs");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "StaffTiers");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "CustomerTiers");

            migrationBuilder.DropTable(
                name: "ServiceOrders");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "RetailOrders");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "VoucherCampaigns");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "VoucherRedemptions");

            migrationBuilder.DropTable(
                name: "VoucherApprovalRequests");
        }
    }
}
