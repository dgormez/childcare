using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddSepaDirectDebit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SepaBatchId",
                schema: "tenant_template",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SepaMandateReferenceUsed",
                schema: "tenant_template",
                table: "invoices",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SepaReturnReason",
                schema: "tenant_template",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SepaRevokedAt",
                schema: "tenant_template",
                table: "contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sepa_batches",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalCents = table.Column<int>(type: "integer", nullable: false),
                    InvoiceCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sepa_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sepa_batches_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_SepaBatchId",
                schema: "tenant_template",
                table: "invoices",
                column: "SepaBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_sepa_batches_LocationId_GeneratedAt",
                schema: "tenant_template",
                table: "sepa_batches",
                columns: new[] { "LocationId", "GeneratedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_sepa_batches_SepaBatchId",
                schema: "tenant_template",
                table: "invoices",
                column: "SepaBatchId",
                principalSchema: "tenant_template",
                principalTable: "sepa_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoices_sepa_batches_SepaBatchId",
                schema: "tenant_template",
                table: "invoices");

            migrationBuilder.DropTable(
                name: "sepa_batches",
                schema: "tenant_template");

            migrationBuilder.DropIndex(
                name: "IX_invoices_SepaBatchId",
                schema: "tenant_template",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "SepaBatchId",
                schema: "tenant_template",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "SepaMandateReferenceUsed",
                schema: "tenant_template",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "SepaReturnReason",
                schema: "tenant_template",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "SepaRevokedAt",
                schema: "tenant_template",
                table: "contracts");
        }
    }
}
