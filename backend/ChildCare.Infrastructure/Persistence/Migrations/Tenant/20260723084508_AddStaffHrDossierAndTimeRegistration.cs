using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddStaffHrDossierAndTimeRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "TimeEntryFunctions",
                schema: "tenant_template",
                table: "staff_profiles",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "staff_documents",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ObjectPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_documents_staff_profiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalSchema: "tenant_template",
                        principalTable: "staff_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_time_entries",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClockedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClockedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Function = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnlockedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_time_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_time_entries_groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "tenant_template",
                        principalTable: "groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_staff_time_entries_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_time_entries_staff_profiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalSchema: "tenant_template",
                        principalTable: "staff_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_documents_DocumentType_ValidUntil",
                schema: "tenant_template",
                table: "staff_documents",
                columns: new[] { "DocumentType", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_documents_StaffProfileId",
                schema: "tenant_template",
                table: "staff_documents",
                column: "StaffProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_time_entries_GroupId",
                schema: "tenant_template",
                table: "staff_time_entries",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_time_entries_LocationId_ClockedInAt",
                schema: "tenant_template",
                table: "staff_time_entries",
                columns: new[] { "LocationId", "ClockedInAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_time_entries_StaffProfileId_ClockedOutAt",
                schema: "tenant_template",
                table: "staff_time_entries",
                columns: new[] { "StaffProfileId", "ClockedOutAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_documents",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "staff_time_entries",
                schema: "tenant_template");

            migrationBuilder.DropColumn(
                name: "TimeEntryFunctions",
                schema: "tenant_template",
                table: "staff_profiles");
        }
    }
}
