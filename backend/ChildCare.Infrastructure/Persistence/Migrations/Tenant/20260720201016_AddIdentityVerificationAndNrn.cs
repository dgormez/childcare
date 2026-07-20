using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddIdentityVerificationAndNrn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstIdVerifiedAt",
                schema: "tenant_template",
                table: "contacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstIdVerifiedByEmail",
                schema: "tenant_template",
                table: "contacts",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirstIdVerifiedByUserId",
                schema: "tenant_template",
                table: "contacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdDocumentNote",
                schema: "tenant_template",
                table: "contacts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdDocumentType",
                schema: "tenant_template",
                table: "contacts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IdVerifiedAt",
                schema: "tenant_template",
                table: "contacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdVerifiedByEmail",
                schema: "tenant_template",
                table: "contacts",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdVerifiedByUserId",
                schema: "tenant_template",
                table: "contacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedNrn",
                schema: "tenant_template",
                table: "children",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstIdVerifiedAt",
                schema: "tenant_template",
                table: "children",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstIdVerifiedByEmail",
                schema: "tenant_template",
                table: "children",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirstIdVerifiedByUserId",
                schema: "tenant_template",
                table: "children",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdDocumentNote",
                schema: "tenant_template",
                table: "children",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdDocumentType",
                schema: "tenant_template",
                table: "children",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IdVerifiedAt",
                schema: "tenant_template",
                table: "children",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdVerifiedByEmail",
                schema: "tenant_template",
                table: "children",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdVerifiedByUserId",
                schema: "tenant_template",
                table: "children",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NrnLast4",
                schema: "tenant_template",
                table: "children",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstIdVerifiedAt",
                schema: "tenant_template",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "FirstIdVerifiedByEmail",
                schema: "tenant_template",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "FirstIdVerifiedByUserId",
                schema: "tenant_template",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "IdDocumentNote",
                schema: "tenant_template",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "IdDocumentType",
                schema: "tenant_template",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "IdVerifiedAt",
                schema: "tenant_template",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "IdVerifiedByEmail",
                schema: "tenant_template",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "IdVerifiedByUserId",
                schema: "tenant_template",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "EncryptedNrn",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "FirstIdVerifiedAt",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "FirstIdVerifiedByEmail",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "FirstIdVerifiedByUserId",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "IdDocumentNote",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "IdDocumentType",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "IdVerifiedAt",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "IdVerifiedByEmail",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "IdVerifiedByUserId",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "NrnLast4",
                schema: "tenant_template",
                table: "children");
        }
    }
}
