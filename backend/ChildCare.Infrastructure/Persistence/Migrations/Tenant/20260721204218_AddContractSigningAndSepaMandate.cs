using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddContractSigningAndSepaMandate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SepaAuthorisedAt",
                schema: "tenant_template",
                table: "contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SepaIbanEncrypted",
                schema: "tenant_template",
                table: "contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SepaIbanLast4",
                schema: "tenant_template",
                table: "contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SepaMandateReference",
                schema: "tenant_template",
                table: "contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureData",
                schema: "tenant_template",
                table: "contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureType",
                schema: "tenant_template",
                table: "contracts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAt",
                schema: "tenant_template",
                table: "contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedByIp",
                schema: "tenant_template",
                table: "contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SigningToken",
                schema: "tenant_template",
                table: "contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SigningTokenExpiresAt",
                schema: "tenant_template",
                table: "contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contracts_SepaMandateReference",
                schema: "tenant_template",
                table: "contracts",
                column: "SepaMandateReference",
                unique: true,
                filter: "\"SepaMandateReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_SigningToken",
                schema: "tenant_template",
                table: "contracts",
                column: "SigningToken",
                unique: true,
                filter: "\"SigningToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contracts_SepaMandateReference",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropIndex(
                name: "IX_contracts_SigningToken",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SepaAuthorisedAt",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SepaIbanEncrypted",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SepaIbanLast4",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SepaMandateReference",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SignatureData",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SignatureType",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SignedAt",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SignedByIp",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SigningToken",
                schema: "tenant_template",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SigningTokenExpiresAt",
                schema: "tenant_template",
                table: "contracts");
        }
    }
}
