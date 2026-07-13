using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddVaccineAndHealthRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "health_records",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    AttachmentObjectPath = table.Column<string>(type: "text", nullable: true),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_health_records_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_health_records_users_RecordedBy",
                        column: x => x.RecordedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "vaccine_records",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    VaccineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DoseNumber = table.Column<int>(type: "integer", nullable: true),
                    AdministeredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AdministeredBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vaccine_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vaccine_records_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vaccine_records_users_RecordedBy",
                        column: x => x.RecordedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id");
                });

            // Backfill any existing vaccination_records rows (feature 006) into the new, richer
            // vaccine_records schema before dropping the old table (research.md R1). New columns
            // this legacy table never had (DoseNumber, AdministeredBy, Notes, RecordedBy) are left
            // null; UpdatedAt defaults to CreatedAt since the legacy table had no edit concept.
            migrationBuilder.Sql(@"
                INSERT INTO ""tenant_template"".""vaccine_records""
                    (""Id"", ""ChildId"", ""VaccineName"", ""AdministeredOn"", ""NextDueDate"", ""CreatedAt"", ""UpdatedAt"")
                SELECT ""Id"", ""ChildId"", ""VaccineName"", ""DateAdministered"", ""NextDueDate"", ""CreatedAt"", ""CreatedAt""
                FROM ""tenant_template"".""vaccination_records"";
            ");

            migrationBuilder.DropTable(
                name: "vaccination_records",
                schema: "tenant_template");

            migrationBuilder.CreateIndex(
                name: "IX_health_records_ChildId",
                schema: "tenant_template",
                table: "health_records",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_health_records_RecordedBy",
                schema: "tenant_template",
                table: "health_records",
                column: "RecordedBy");

            migrationBuilder.CreateIndex(
                name: "IX_vaccine_records_ChildId",
                schema: "tenant_template",
                table: "vaccine_records",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_vaccine_records_NextDueDate",
                schema: "tenant_template",
                table: "vaccine_records",
                column: "NextDueDate",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vaccine_records_RecordedBy",
                schema: "tenant_template",
                table: "vaccine_records",
                column: "RecordedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "health_records",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "vaccine_records",
                schema: "tenant_template");

            migrationBuilder.CreateTable(
                name: "vaccination_records",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateAdministered = table.Column<DateOnly>(type: "date", nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    VaccineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vaccination_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vaccination_records_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vaccination_records_ChildId",
                schema: "tenant_template",
                table: "vaccination_records",
                column: "ChildId");
        }
    }
}
