using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddWaitingListEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "waiting_list_entries",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildFirstName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChildLastName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waiting_list_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_waiting_list_entries_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_waiting_list_entries_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_waiting_list_entries_ChildId",
                schema: "tenant_template",
                table: "waiting_list_entries",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_waiting_list_entries_LocationId_Status_Priority",
                schema: "tenant_template",
                table: "waiting_list_entries",
                columns: new[] { "LocationId", "Status", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waiting_list_entries",
                schema: "tenant_template");
        }
    }
}
