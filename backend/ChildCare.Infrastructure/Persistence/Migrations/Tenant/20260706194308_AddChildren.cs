using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddChildren : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "children",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    ProfilePhotoObjectPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Nationality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AllergiesDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AllergySeverity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MedicalConditions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DietaryRestrictions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    GpName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GpPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    HealthInsuranceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Kindcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DeactivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_children", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Locale = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_groups_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vaccination_records",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    VaccineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateAdministered = table.Column<DateOnly>(type: "date", nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "child_contacts",
                schema: "tenant_template",
                columns: table => new
                {
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Relationship = table.Column<int>(type: "integer", nullable: false),
                    CanPickup = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_child_contacts", x => new { x.ChildId, x.ContactId });
                    table.ForeignKey(
                        name: "FK_child_contacts_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_child_contacts_contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "tenant_template",
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "child_group_assignments",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_child_group_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_child_group_assignments_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_child_group_assignments_groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "tenant_template",
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_child_contacts_ContactId",
                schema: "tenant_template",
                table: "child_contacts",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_child_group_assignments_ChildId_EndDate",
                schema: "tenant_template",
                table: "child_group_assignments",
                columns: new[] { "ChildId", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_child_group_assignments_GroupId",
                schema: "tenant_template",
                table: "child_group_assignments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_children_DeactivatedAt",
                schema: "tenant_template",
                table: "children",
                column: "DeactivatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_groups_LocationId",
                schema: "tenant_template",
                table: "groups",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_vaccination_records_ChildId",
                schema: "tenant_template",
                table: "vaccination_records",
                column: "ChildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "child_contacts",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "child_group_assignments",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "vaccination_records",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "contacts",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "groups",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "children",
                schema: "tenant_template");
        }
    }
}
