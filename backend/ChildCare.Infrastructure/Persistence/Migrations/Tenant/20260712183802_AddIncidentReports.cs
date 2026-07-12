using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddIncidentReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incident_reports",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LocationDetail = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    InjuryType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FirstAidGiven = table.Column<string>(type: "text", nullable: true),
                    DoctorCalled = table.Column<bool>(type: "boolean", nullable: false),
                    DoctorNotes = table.Column<string>(type: "text", nullable: true),
                    ParentNotified = table.Column<bool>(type: "boolean", nullable: false),
                    ParentNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParentNotifiedHow = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReportedBy = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    Witnesses = table.Column<string>(type: "text", nullable: true),
                    FollowUp = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incident_reports_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_incident_reports_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incident_reports_ChildId_OccurredAt",
                schema: "tenant_template",
                table: "incident_reports",
                columns: new[] { "ChildId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_incident_reports_LocationId_OccurredAt",
                schema: "tenant_template",
                table: "incident_reports",
                columns: new[] { "LocationId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_reports",
                schema: "tenant_template");
        }
    }
}
