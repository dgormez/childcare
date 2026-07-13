using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddVaccineCatalogAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentObjectPath",
                schema: "tenant_template",
                table: "vaccine_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomVaccineEntryId",
                schema: "tenant_template",
                table: "vaccine_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VaccineTypeId",
                schema: "tenant_template",
                table: "vaccine_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_custom_vaccine_entries",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_custom_vaccine_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vaccine_records_CustomVaccineEntryId",
                schema: "tenant_template",
                table: "vaccine_records",
                column: "CustomVaccineEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_vaccine_records_VaccineTypeId",
                schema: "tenant_template",
                table: "vaccine_records",
                column: "VaccineTypeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_vaccine_records_vaccine_reference_exclusive",
                schema: "tenant_template",
                table: "vaccine_records",
                sql: "\"VaccineTypeId\" IS NULL OR \"CustomVaccineEntryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_custom_vaccine_entries_NormalizedName",
                schema: "tenant_template",
                table: "tenant_custom_vaccine_entries",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_vaccine_records_tenant_custom_vaccine_entries_CustomVaccine~",
                schema: "tenant_template",
                table: "vaccine_records",
                column: "CustomVaccineEntryId",
                principalSchema: "tenant_template",
                principalTable: "tenant_custom_vaccine_entries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vaccine_records_tenant_custom_vaccine_entries_CustomVaccine~",
                schema: "tenant_template",
                table: "vaccine_records");

            migrationBuilder.DropTable(
                name: "tenant_custom_vaccine_entries",
                schema: "tenant_template");

            migrationBuilder.DropIndex(
                name: "IX_vaccine_records_CustomVaccineEntryId",
                schema: "tenant_template",
                table: "vaccine_records");

            migrationBuilder.DropIndex(
                name: "IX_vaccine_records_VaccineTypeId",
                schema: "tenant_template",
                table: "vaccine_records");

            migrationBuilder.DropCheckConstraint(
                name: "CK_vaccine_records_vaccine_reference_exclusive",
                schema: "tenant_template",
                table: "vaccine_records");

            migrationBuilder.DropColumn(
                name: "AttachmentObjectPath",
                schema: "tenant_template",
                table: "vaccine_records");

            migrationBuilder.DropColumn(
                name: "CustomVaccineEntryId",
                schema: "tenant_template",
                table: "vaccine_records");

            migrationBuilder.DropColumn(
                name: "VaccineTypeId",
                schema: "tenant_template",
                table: "vaccine_records");
        }
    }
}
