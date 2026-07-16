using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddFiscalAttestations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fiscal_attestations",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    Periods = table.Column<string>(type: "jsonb", nullable: false),
                    TotalAmountCents = table.Column<int>(type: "integer", nullable: false),
                    PdfObjectPath = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_attestations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fiscal_attestations_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fiscal_attestations_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_attestations_ChildId_LocationId_TaxYear",
                schema: "tenant_template",
                table: "fiscal_attestations",
                columns: new[] { "ChildId", "LocationId", "TaxYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_attestations_LocationId",
                schema: "tenant_template",
                table: "fiscal_attestations",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiscal_attestations",
                schema: "tenant_template");
        }
    }
}
