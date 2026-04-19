using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGarage.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHandoverAndSeedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE [Roles] DROP CONSTRAINT IF EXISTS CK_Roles_RoleCode;
                
                ALTER TABLE [Roles] ADD CONSTRAINT CK_Roles_RoleCode 
                CHECK ([RoleCode]='ACCOUNTANT' OR [RoleCode]='QUALITY_CONTROL' OR [RoleCode]='FOREMAN' OR [RoleCode]='TEAM_LEADER' OR [RoleCode]='MANAGER' OR [RoleCode]='RECEPTIONIST' OR [RoleCode]='TECH' OR [RoleCode]='ADMIN' OR [RoleCode]='STORE_KEEPER');
                
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleCode = 'STORE_KEEPER')
                BEGIN
                    INSERT INTO Roles (Id, RoleName, RoleCode, Scope, Description, IsActive, IsDeleted, CreatedAt, UpdatedAt, CreatedBy)
                    VALUES (3, N'Thủ kho', 'STORE_KEEPER', 'BRANCH', N'Quản lý vật tư, phụ tùng và điều phối kho.', 1, 0, '2024-01-01', '2024-01-01', 1)
                END
                ELSE
                BEGIN
                    UPDATE Roles SET RoleName = N'Thủ kho', Scope = 'BRANCH' WHERE RoleCode = 'STORE_KEEPER';
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM Roles WHERE RoleCode = 'STORE_KEEPER'");
        }
    }
}
