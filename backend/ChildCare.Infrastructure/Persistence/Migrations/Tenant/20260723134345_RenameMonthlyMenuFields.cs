using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RenameMonthlyMenuFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MainCourse really is the lunch meal — rename to preserve existing content.
            migrationBuilder.RenameColumn(
                name: "MainCourse",
                schema: "tenant_template",
                table: "monthly_menu_days",
                newName: "LunchMeal");

            // Soup and Dessert are dropped, not repurposed — this content isn't an alternative
            // lunch meal or a 3pm snack, so it isn't carried forward under the new columns
            // (the auto-generated migration would have renamed Soup->Snack and Dessert-> Alt.
            // lunch by column-order heuristics alone, silently mislabeling old data).
            migrationBuilder.DropColumn(
                name: "Soup",
                schema: "tenant_template",
                table: "monthly_menu_days");

            migrationBuilder.DropColumn(
                name: "Dessert",
                schema: "tenant_template",
                table: "monthly_menu_days");

            migrationBuilder.AddColumn<string>(
                name: "AlternativeLunchMeal",
                schema: "tenant_template",
                table: "monthly_menu_days",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Snack",
                schema: "tenant_template",
                table: "monthly_menu_days",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlternativeLunchMeal",
                schema: "tenant_template",
                table: "monthly_menu_days");

            migrationBuilder.DropColumn(
                name: "Snack",
                schema: "tenant_template",
                table: "monthly_menu_days");

            migrationBuilder.AddColumn<string>(
                name: "Soup",
                schema: "tenant_template",
                table: "monthly_menu_days",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dessert",
                schema: "tenant_template",
                table: "monthly_menu_days",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "LunchMeal",
                schema: "tenant_template",
                table: "monthly_menu_days",
                newName: "MainCourse");
        }
    }
}
