using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddGroupActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "group_activities",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    RecordedByDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_activities_device_pairings_RecordedByDeviceId",
                        column: x => x.RecordedByDeviceId,
                        principalSchema: "tenant_template",
                        principalTable: "device_pairings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_activities_groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "tenant_template",
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_activities_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_activity_photos",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectPath = table.Column<string>(type: "text", nullable: false),
                    ThumbnailObjectPath = table.Column<string>(type: "text", nullable: false),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_activity_photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_activity_photos_group_activities_GroupActivityId",
                        column: x => x.GroupActivityId,
                        principalSchema: "tenant_template",
                        principalTable: "group_activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_activities_GroupId_OccurredAt",
                schema: "tenant_template",
                table: "group_activities",
                columns: new[] { "GroupId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_group_activities_LocationId",
                schema: "tenant_template",
                table: "group_activities",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_group_activities_RecordedByDeviceId",
                schema: "tenant_template",
                table: "group_activities",
                column: "RecordedByDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_group_activity_photos_GroupActivityId",
                schema: "tenant_template",
                table: "group_activity_photos",
                column: "GroupActivityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_activity_photos",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "group_activities",
                schema: "tenant_template");
        }
    }
}
