using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPediatricianContactToChild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PediatricianName",
                schema: "tenant_template",
                table: "children",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PediatricianPhone",
                schema: "tenant_template",
                table: "children",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PediatricianName",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "PediatricianPhone",
                schema: "tenant_template",
                table: "children");
        }
    }
}
