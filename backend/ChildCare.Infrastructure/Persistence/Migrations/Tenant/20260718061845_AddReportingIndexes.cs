using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_LocationId",
                schema: "tenant_template",
                table: "invoices");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_LocationId_PeriodMonth",
                schema: "tenant_template",
                table: "invoices",
                columns: new[] { "LocationId", "PeriodMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Status_DueDate",
                schema: "tenant_template",
                table: "invoices",
                columns: new[] { "Status", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_LocationId_PeriodMonth",
                schema: "tenant_template",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_Status_DueDate",
                schema: "tenant_template",
                table: "invoices");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_LocationId",
                schema: "tenant_template",
                table: "invoices",
                column: "LocationId");
        }
    }
}
