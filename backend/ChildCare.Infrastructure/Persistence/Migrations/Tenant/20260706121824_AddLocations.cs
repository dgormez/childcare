using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locations",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    MaxCapacity = table.Column<int>(type: "integer", nullable: false),
                    NaamLocatie = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Dossiernummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Verantwoordelijke = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FlexPermission = table.Column<bool>(type: "boolean", nullable: false),
                    BoPermission = table.Column<bool>(type: "boolean", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.Id);
                    table.CheckConstraint("CK_locations_max_capacity", "\"MaxCapacity\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_locations_DeactivatedAt",
                schema: "tenant_template",
                table: "locations",
                column: "DeactivatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "locations",
                schema: "tenant_template");
        }
    }
}
