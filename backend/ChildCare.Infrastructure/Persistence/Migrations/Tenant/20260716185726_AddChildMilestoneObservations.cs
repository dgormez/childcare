using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddChildMilestoneObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "child_milestone_observations",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ObservedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    ObservedBy = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_child_milestone_observations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_child_milestone_observations_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_child_milestone_observations_ChildId",
                schema: "tenant_template",
                table: "child_milestone_observations",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_child_milestone_observations_ChildId_MilestoneId_CreatedAt",
                schema: "tenant_template",
                table: "child_milestone_observations",
                columns: new[] { "ChildId", "MilestoneId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "child_milestone_observations",
                schema: "tenant_template");
        }
    }
}
