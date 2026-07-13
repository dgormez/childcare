using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddChildMealPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "child_meal_preferences",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texture = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DietaryType = table.Column<string[]>(type: "text[]", nullable: false),
                    PortionSize = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AdditionalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_child_meal_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_child_meal_preferences_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_child_meal_preferences_users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_child_meal_preferences_ChildId",
                schema: "tenant_template",
                table: "child_meal_preferences",
                column: "ChildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_child_meal_preferences_UpdatedBy",
                schema: "tenant_template",
                table: "child_meal_preferences",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "child_meal_preferences",
                schema: "tenant_template");
        }
    }
}
