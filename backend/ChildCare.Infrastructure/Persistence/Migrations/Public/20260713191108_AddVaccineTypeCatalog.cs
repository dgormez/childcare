using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Public
{
    /// <inheritdoc />
    public partial class AddVaccineTypeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vaccine_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vaccine_types", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vaccine_types_Category_SortOrder",
                table: "vaccine_types",
                columns: new[] { "Category", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_vaccine_types_IsActive",
                table: "vaccine_types",
                column: "IsActive");

            // Seed data (data-model.md, research.md R7) — the Vlaamse Departement Zorg
            // basisvaccinatieschema plus the recommended-but-not-free set relevant to
            // daycare-age children. Reviewed and applied manually like any other migration
            // (constitution Principle VI) — not auto-applied in production.
            var seededAt = new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);
            migrationBuilder.InsertData(
                table: "vaccine_types",
                columns: new[] { "Id", "Name", "Category", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("b1e6f0a0-0001-4a00-8000-000000000001"), "DTPa-IPV-Hib-HepB", "basisvaccinatieschema", 1, true, seededAt, seededAt },
                    { new Guid("b1e6f0a0-0001-4a00-8000-000000000002"), "Pneumokokken (PCV)", "basisvaccinatieschema", 2, true, seededAt, seededAt },
                    { new Guid("b1e6f0a0-0001-4a00-8000-000000000003"), "BMR (bof, mazelen, rodehond)", "basisvaccinatieschema", 3, true, seededAt, seededAt },
                    { new Guid("b1e6f0a0-0001-4a00-8000-000000000004"), "MenACWY", "basisvaccinatieschema", 4, true, seededAt, seededAt },
                    { new Guid("b1e6f0a0-0001-4a00-8000-000000000005"), "HPV", "basisvaccinatieschema", 5, true, seededAt, seededAt },
                    { new Guid("b1e6f0a0-0001-4a00-8000-000000000006"), "RSV (zuigelingen)", "aanbevolen_niet_gratis", 1, true, seededAt, seededAt },
                    { new Guid("b1e6f0a0-0001-4a00-8000-000000000007"), "MenB", "aanbevolen_niet_gratis", 2, true, seededAt, seededAt },
                    { new Guid("b1e6f0a0-0001-4a00-8000-000000000008"), "Hepatitis A", "aanbevolen_niet_gratis", 3, true, seededAt, seededAt },
                    { new Guid("b1e6f0a0-0001-4a00-8000-000000000009"), "Waterpokken (varicella)", "aanbevolen_niet_gratis", 4, true, seededAt, seededAt },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vaccine_types");
        }
    }
}
