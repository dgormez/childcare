using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddRoomShiftsAndDevicePairings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PinFailedAttempts",
                schema: "tenant_template",
                table: "staff_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinFirstFailedAttemptAt",
                schema: "tenant_template",
                table: "staff_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                schema: "tenant_template",
                table: "staff_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinLockedUntil",
                schema: "tenant_template",
                table: "staff_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "device_pairings",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    DirectorOverridePinHash = table.Column<string>(type: "text", nullable: false),
                    TokenIssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TokenVersion = table.Column<int>(type: "integer", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PairedByTenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverridePinFailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    OverridePinFirstFailedAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OverridePinLockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_pairings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_pairings_groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "tenant_template",
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_device_pairings_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_device_pairings_users_PairedByTenantUserId",
                        column: x => x.PairedByTenantUserId,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_shifts",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    DevicePairingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedReason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_room_shifts_device_pairings_DevicePairingId",
                        column: x => x.DevicePairingId,
                        principalSchema: "tenant_template",
                        principalTable: "device_pairings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_room_shifts_groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "tenant_template",
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_room_shifts_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_room_shifts_staff_profiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalSchema: "tenant_template",
                        principalTable: "staff_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_pairings_GroupId",
                schema: "tenant_template",
                table: "device_pairings",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_device_pairings_LocationId",
                schema: "tenant_template",
                table: "device_pairings",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_device_pairings_PairedByTenantUserId",
                schema: "tenant_template",
                table: "device_pairings",
                column: "PairedByTenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_device_pairings_RevokedAt",
                schema: "tenant_template",
                table: "device_pairings",
                column: "RevokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_room_shifts_CheckedOutAt_LocationId_GroupId",
                schema: "tenant_template",
                table: "room_shifts",
                columns: new[] { "CheckedOutAt", "LocationId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_room_shifts_CheckedOutAt_StaffProfileId",
                schema: "tenant_template",
                table: "room_shifts",
                columns: new[] { "CheckedOutAt", "StaffProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_room_shifts_DevicePairingId",
                schema: "tenant_template",
                table: "room_shifts",
                column: "DevicePairingId");

            migrationBuilder.CreateIndex(
                name: "IX_room_shifts_GroupId",
                schema: "tenant_template",
                table: "room_shifts",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_room_shifts_LocationId",
                schema: "tenant_template",
                table: "room_shifts",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_room_shifts_StaffProfileId",
                schema: "tenant_template",
                table: "room_shifts",
                column: "StaffProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_shifts",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "device_pairings",
                schema: "tenant_template");

            migrationBuilder.DropColumn(
                name: "PinFailedAttempts",
                schema: "tenant_template",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "PinFirstFailedAttemptAt",
                schema: "tenant_template",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "PinHash",
                schema: "tenant_template",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "PinLockedUntil",
                schema: "tenant_template",
                table: "staff_profiles");
        }
    }
}
