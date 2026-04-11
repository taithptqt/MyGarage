using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGarage.Migrations
{
    public partial class AddChatbotTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotSessions", x => x.Id);
                    table.CheckConstraint("CK_ChatbotSessions_IntentState_IsJson", "[IntentState] IS NULL OR ISJSON([IntentState]) = 1");
                    table.CheckConstraint("CK_ChatbotSessions_SessionToken_NotEmpty", "LEN([SessionToken]) > 0");
                    table.CheckConstraint("CK_ChatbotSessions_Status", "[Status] IN ('ACTIVE','IDLE','HANDED_OFF','EXPIRED','CLOSED','ARCHIVED')");
                    table.ForeignKey(
                        name: "FK_ChatbotSessions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatbotSessions_Conversations_HandoffConversationId",
                        column: x => x.HandoffConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatbotSessions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatbotSessions_ServiceCategories_ContextServiceCategoryId",
                        column: x => x.ContextServiceCategoryId,
                        principalTable: "ServiceCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatbotSessions_Vehicles_ContextVehicleId",
                        column: x => x.ContextVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_ChatbotActionLogs_ChatbotSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ChatbotSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_ChatbotMessages_ChatbotSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ChatbotSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatbotActionLogs");

            migrationBuilder.DropTable(
                name: "ChatbotMessages");

            migrationBuilder.DropTable(
                name: "ChatbotSessions");
        }
    }
}
