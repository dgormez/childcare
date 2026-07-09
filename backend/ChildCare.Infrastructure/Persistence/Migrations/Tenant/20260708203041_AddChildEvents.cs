using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddChildEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "child_events",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    VisibleToParent = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedBy = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    AdministeredBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_child_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_child_events_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_child_events_device_pairings_RecordedByDeviceId",
                        column: x => x.RecordedByDeviceId,
                        principalSchema: "tenant_template",
                        principalTable: "device_pairings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_child_events_groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "tenant_template",
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_child_events_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_child_events_ChildId_EventType_OccurredAt",
                schema: "tenant_template",
                table: "child_events",
                columns: new[] { "ChildId", "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_child_events_ChildId_OccurredAt",
                schema: "tenant_template",
                table: "child_events",
                columns: new[] { "ChildId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_child_events_GroupId",
                schema: "tenant_template",
                table: "child_events",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_child_events_LocationId",
                schema: "tenant_template",
                table: "child_events",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_child_events_RecordedByDeviceId",
                schema: "tenant_template",
                table: "child_events",
                column: "RecordedByDeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "child_events",
                schema: "tenant_template");
        }
    }
}
