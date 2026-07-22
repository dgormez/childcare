using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddStaffAppPersonalRotaAndLeave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoverStaffId",
                schema: "tenant_template",
                table: "staff_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "tenant_template",
                table: "staff_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                schema: "tenant_template",
                table: "staff_schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "tenant_template",
                table: "staff_schedules",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                schema: "tenant_template",
                table: "staff_schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "tenant_template",
                table: "staff_schedules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "scheduled");

            // data-model.md's Migration notes / research.md R3: backfill Status from the
            // existing IsAbsent boolean before dropping it — safe in-place rename since no
            // production tenant data exists yet for this pre-revenue project.
            migrationBuilder.Sql(
                """
                UPDATE "tenant_template"."staff_schedules"
                SET "Status" = CASE WHEN "IsAbsent" THEN 'absent' ELSE 'scheduled' END;
                """);

            migrationBuilder.DropColumn(
                name: "IsAbsent",
                schema: "tenant_template",
                table: "staff_schedules");

            migrationBuilder.AddColumn<string[]>(
                name: "ContractedDays",
                schema: "tenant_template",
                table: "staff_profiles",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "PushToken",
                schema: "tenant_template",
                table: "staff_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "staff_leave_requests",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DateFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    DateTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_leave_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_leave_requests_staff_profiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalSchema: "tenant_template",
                        principalTable: "staff_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_leave_requests_StaffProfileId_CreatedAt",
                schema: "tenant_template",
                table: "staff_leave_requests",
                columns: new[] { "StaffProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_leave_requests_Status",
                schema: "tenant_template",
                table: "staff_leave_requests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_leave_requests",
                schema: "tenant_template");

            migrationBuilder.DropColumn(
                name: "CoverStaffId",
                schema: "tenant_template",
                table: "staff_schedules");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "tenant_template",
                table: "staff_schedules");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                schema: "tenant_template",
                table: "staff_schedules");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "tenant_template",
                table: "staff_schedules");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                schema: "tenant_template",
                table: "staff_schedules");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "tenant_template",
                table: "staff_schedules");

            migrationBuilder.DropColumn(
                name: "ContractedDays",
                schema: "tenant_template",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "PushToken",
                schema: "tenant_template",
                table: "staff_profiles");

            migrationBuilder.AddColumn<bool>(
                name: "IsAbsent",
                schema: "tenant_template",
                table: "staff_schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
