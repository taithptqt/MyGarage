using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGarage.Migrations
{
    public partial class AddQuotationAccessLinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotationAccessLinks");
        }
    }
}

