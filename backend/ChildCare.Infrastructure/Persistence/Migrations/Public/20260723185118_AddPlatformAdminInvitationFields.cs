using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Public
{
    /// <inheritdoc />
    public partial class AddPlatformAdminInvitationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByEmail",
                table: "invitations",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "invitations",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "nl");

            migrationBuilder.AddColumn<string>(
                name: "OrganisationNameNote",
                table: "invitations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevokedByEmail",
                table: "invitations",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevokedByUserId",
                table: "invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_invitations_locale",
                table: "invitations",
                sql: "\"Locale\" IN ('nl','fr','en')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_invitations_locale",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "CreatedByEmail",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "Locale",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "OrganisationNameNote",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "RevokedByEmail",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "RevokedByUserId",
                table: "invitations");
        }
    }
}
