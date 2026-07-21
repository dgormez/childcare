using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddDigitalEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceCode",
                schema: "tenant_template",
                table: "waiting_list_entries",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "tenant_template",
                table: "waiting_list_entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "directorentered");

            migrationBuilder.AddColumn<string>(
                name: "SubmittedLocale",
                schema: "tenant_template",
                table: "waiting_list_entries",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TourInvitationSentAt",
                schema: "tenant_template",
                table: "waiting_list_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TourInvitationStatus",
                schema: "tenant_template",
                table: "waiting_list_entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "notsent");

            migrationBuilder.AddColumn<string>(
                name: "TourOutcome",
                schema: "tenant_template",
                table: "waiting_list_entries",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TourProposedAt",
                schema: "tenant_template",
                table: "waiting_list_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultEnrollmentLocale",
                schema: "tenant_template",
                table: "locations",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "nl");

            migrationBuilder.AddColumn<bool>(
                name: "PublicEnrollmentEnabled",
                schema: "tenant_template",
                table: "locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicEnrollmentSlug",
                schema: "tenant_template",
                table: "locations",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            // Backfill: every pre-existing location gets a slug derived from its name (research.md
            // R1/data-model.md) before the column is locked to NOT NULL + unique below — a plain
            // static AddColumn default can't produce a per-row-unique value. Non-alphanumeric runs
            // become a single hyphen; duplicates (or an empty name) get a numeric/id-based
            // disambiguating suffix so the unique index below can never fail on rollout.
            migrationBuilder.Sql("""
                WITH slugged AS (
                    SELECT "Id",
                           NULLIF(regexp_replace(lower(trim(both '-' from regexp_replace("Name", '[^a-zA-Z0-9]+', '-', 'g'))), '-+', '-', 'g'), '') AS base_slug
                    FROM "tenant_template"."locations"
                ),
                numbered AS (
                    SELECT "Id",
                           COALESCE(base_slug, 'location') AS base_slug,
                           ROW_NUMBER() OVER (PARTITION BY COALESCE(base_slug, 'location') ORDER BY "Id") AS rn
                    FROM slugged
                )
                UPDATE "tenant_template"."locations" AS l
                SET "PublicEnrollmentSlug" = CASE WHEN n.rn = 1 THEN n.base_slug ELSE n.base_slug || '-' || n.rn::text END
                FROM numbered n
                WHERE l."Id" = n."Id";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PublicEnrollmentSlug",
                schema: "tenant_template",
                table: "locations",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_waiting_list_entries_LocationId_ChildFirstName_ChildLastNam~",
                schema: "tenant_template",
                table: "waiting_list_entries",
                columns: new[] { "LocationId", "ChildFirstName", "ChildLastName", "DateOfBirth" });

            migrationBuilder.CreateIndex(
                name: "IX_waiting_list_entries_ReferenceCode",
                schema: "tenant_template",
                table: "waiting_list_entries",
                column: "ReferenceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locations_PublicEnrollmentSlug",
                schema: "tenant_template",
                table: "locations",
                column: "PublicEnrollmentSlug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_waiting_list_entries_LocationId_ChildFirstName_ChildLastNam~",
                schema: "tenant_template",
                table: "waiting_list_entries");

            migrationBuilder.DropIndex(
                name: "IX_waiting_list_entries_ReferenceCode",
                schema: "tenant_template",
                table: "waiting_list_entries");

            migrationBuilder.DropIndex(
                name: "IX_locations_PublicEnrollmentSlug",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "ReferenceCode",
                schema: "tenant_template",
                table: "waiting_list_entries");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "tenant_template",
                table: "waiting_list_entries");

            migrationBuilder.DropColumn(
                name: "SubmittedLocale",
                schema: "tenant_template",
                table: "waiting_list_entries");

            migrationBuilder.DropColumn(
                name: "TourInvitationSentAt",
                schema: "tenant_template",
                table: "waiting_list_entries");

            migrationBuilder.DropColumn(
                name: "TourInvitationStatus",
                schema: "tenant_template",
                table: "waiting_list_entries");

            migrationBuilder.DropColumn(
                name: "TourOutcome",
                schema: "tenant_template",
                table: "waiting_list_entries");

            migrationBuilder.DropColumn(
                name: "TourProposedAt",
                schema: "tenant_template",
                table: "waiting_list_entries");

            migrationBuilder.DropColumn(
                name: "DefaultEnrollmentLocale",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "PublicEnrollmentEnabled",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "PublicEnrollmentSlug",
                schema: "tenant_template",
                table: "locations");
        }
    }
}
