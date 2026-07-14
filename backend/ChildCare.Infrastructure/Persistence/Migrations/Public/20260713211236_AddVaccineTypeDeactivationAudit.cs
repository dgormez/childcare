using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Public
{
    /// <inheritdoc />
    public partial class AddVaccineTypeDeactivationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "vaccine_types",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivatedByEmail",
                table: "vaccine_types",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeactivatedByUserId",
                table: "vaccine_types",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "vaccine_types");

            migrationBuilder.DropColumn(
                name: "DeactivatedByEmail",
                table: "vaccine_types");

            migrationBuilder.DropColumn(
                name: "DeactivatedByUserId",
                table: "vaccine_types");
        }
    }
}
