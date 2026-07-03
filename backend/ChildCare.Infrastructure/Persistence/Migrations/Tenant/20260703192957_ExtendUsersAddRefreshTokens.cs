using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ExtendUsersAddRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppleId",
                schema: "tenant_template",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerificationExpiry",
                schema: "tenant_template",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationToken",
                schema: "tenant_template",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                schema: "tenant_template",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                schema: "tenant_template",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetExpiry",
                schema: "tenant_template",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                schema: "tenant_template",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_TenantUserId",
                        column: x => x.TenantUserId,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TenantUserId",
                schema: "tenant_template",
                table: "refresh_tokens",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_Token",
                schema: "tenant_template",
                table: "refresh_tokens",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "tenant_template");

            migrationBuilder.DropColumn(
                name: "AppleId",
                schema: "tenant_template",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationExpiry",
                schema: "tenant_template",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationToken",
                schema: "tenant_template",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                schema: "tenant_template",
                table: "users");

            migrationBuilder.DropColumn(
                name: "GoogleId",
                schema: "tenant_template",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PasswordResetExpiry",
                schema: "tenant_template",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                schema: "tenant_template",
                table: "users");
        }
    }
}
