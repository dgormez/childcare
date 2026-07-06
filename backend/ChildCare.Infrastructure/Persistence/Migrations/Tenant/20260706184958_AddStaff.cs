using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_profiles",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    QualificationLevel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ProfilePhotoObjectPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeactivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_profiles_users_TenantUserId",
                        column: x => x.TenantUserId,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_invitations",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_invitations_staff_profiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalSchema: "tenant_template",
                        principalTable: "staff_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_location_eligibility",
                schema: "tenant_template",
                columns: table => new
                {
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_location_eligibility", x => new { x.StaffProfileId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_staff_location_eligibility_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_location_eligibility_staff_profiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalSchema: "tenant_template",
                        principalTable: "staff_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_Email",
                schema: "tenant_template",
                table: "staff_invitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_StaffProfileId",
                schema: "tenant_template",
                table: "staff_invitations",
                column: "StaffProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_location_eligibility_LocationId",
                schema: "tenant_template",
                table: "staff_location_eligibility",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_profiles_DeactivatedAt",
                schema: "tenant_template",
                table: "staff_profiles",
                column: "DeactivatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_staff_profiles_TenantUserId",
                schema: "tenant_template",
                table: "staff_profiles",
                column: "TenantUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_invitations",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "staff_location_eligibility",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "staff_profiles",
                schema: "tenant_template");
        }
    }
}
