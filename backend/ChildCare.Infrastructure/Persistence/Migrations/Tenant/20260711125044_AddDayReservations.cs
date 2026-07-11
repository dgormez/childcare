using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddDayReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "day_reservations",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExchangeForDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AbsenceJustified = table.Column<bool>(type: "boolean", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DirectorNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_day_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_day_reservations_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_day_reservations_users_DecidedBy",
                        column: x => x.DecidedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_day_reservations_users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_day_reservations_ChildId_CreatedAt",
                schema: "tenant_template",
                table: "day_reservations",
                columns: new[] { "ChildId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_day_reservations_DecidedBy",
                schema: "tenant_template",
                table: "day_reservations",
                column: "DecidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_day_reservations_RequestedBy",
                schema: "tenant_template",
                table: "day_reservations",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_day_reservations_Status_CreatedAt",
                schema: "tenant_template",
                table: "day_reservations",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "day_reservations",
                schema: "tenant_template");
        }
    }
}
