using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCodaPaymentMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coda_imports",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedDuplicateCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coda_imports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "coda_transactions",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    SenderIbanEncrypted = table.Column<string>(type: "text", nullable: false),
                    SenderIbanLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    SenderName = table.Column<string>(type: "text", nullable: false),
                    Communication = table.Column<string>(type: "text", nullable: false),
                    MatchedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Applied = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coda_transactions", x => x.Id);
                    table.CheckConstraint("CK_coda_transactions_match_type", "\"MatchType\" IN ('Ogm','IbanAmount','Unmatched','Duplicate','ClosedInvoice','Reversal')");
                    table.ForeignKey(
                        name: "FK_coda_transactions_coda_imports_ImportId",
                        column: x => x.ImportId,
                        principalSchema: "tenant_template",
                        principalTable: "coda_imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_coda_transactions_invoices_MatchedInvoiceId",
                        column: x => x.MatchedInvoiceId,
                        principalSchema: "tenant_template",
                        principalTable: "invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_coda_transactions_ImportId",
                schema: "tenant_template",
                table: "coda_transactions",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_coda_transactions_MatchedInvoiceId",
                schema: "tenant_template",
                table: "coda_transactions",
                column: "MatchedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_coda_transactions_ValueDate_AmountCents_SenderIbanLast4",
                schema: "tenant_template",
                table: "coda_transactions",
                columns: new[] { "ValueDate", "AmountCents", "SenderIbanLast4" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coda_transactions",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "coda_imports",
                schema: "tenant_template");
        }
    }
}
