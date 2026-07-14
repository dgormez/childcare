using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddMonthlyMenuVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_monthly_menus_LocationId_Year_Month",
                schema: "tenant_template",
                table: "monthly_menus");

            migrationBuilder.AddColumn<string>(
                name: "Variant",
                schema: "tenant_template",
                table: "monthly_menus",
                type: "text",
                nullable: false,
                defaultValue: "base");

            migrationBuilder.AddColumn<List<string>>(
                name: "MenuVariantPriorityOrder",
                schema: "tenant_template",
                table: "locations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_menus_LocationId_Year_Month_Variant",
                schema: "tenant_template",
                table: "monthly_menus",
                columns: new[] { "LocationId", "Year", "Month", "Variant" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_monthly_menus_LocationId_Year_Month_Variant",
                schema: "tenant_template",
                table: "monthly_menus");

            migrationBuilder.DropColumn(
                name: "Variant",
                schema: "tenant_template",
                table: "monthly_menus");

            migrationBuilder.DropColumn(
                name: "MenuVariantPriorityOrder",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_menus_LocationId_Year_Month",
                schema: "tenant_template",
                table: "monthly_menus",
                columns: new[] { "LocationId", "Year", "Month" },
                unique: true);
        }
    }
}
