using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddSiblingBillingSettingsAndFamilyGroupId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FamilyInvoiceBundlingEnabled",
                schema: "tenant_template",
                table: "locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SiblingDiscountPct",
                schema: "tenant_template",
                table: "locations",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyGroupId",
                schema: "tenant_template",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_FamilyGroupId",
                schema: "tenant_template",
                table: "invoices",
                column: "FamilyGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_FamilyGroupId",
                schema: "tenant_template",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "FamilyInvoiceBundlingEnabled",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "SiblingDiscountPct",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "FamilyGroupId",
                schema: "tenant_template",
                table: "invoices");
        }
    }
}
