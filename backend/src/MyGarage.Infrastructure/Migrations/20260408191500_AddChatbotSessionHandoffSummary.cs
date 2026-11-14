using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGarage.Migrations
{
    public partial class AddChatbotSessionHandoffSummary : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HandoffSummary",
                table: "ChatbotSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatbotSessions_HandoffSummary_IsJson",
                table: "ChatbotSessions",
                sql: "[HandoffSummary] IS NULL OR ISJSON([HandoffSummary]) = 1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatbotSessions_HandoffSummary_IsJson",
                table: "ChatbotSessions");

            migrationBuilder.DropColumn(
                name: "HandoffSummary",
                table: "ChatbotSessions");
        }
    }
}
