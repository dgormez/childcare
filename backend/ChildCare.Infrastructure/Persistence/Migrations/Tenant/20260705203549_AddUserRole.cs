using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows predate the Role column and were all created by organisation
            // onboarding, which only ever creates directors (spec.md Assumption) — the
            // NOT NULL DEFAULT backfills them as 'director' in the same statement.
            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "tenant_template",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "director");

            migrationBuilder.AddCheckConstraint(
                name: "CK_users_role",
                schema: "tenant_template",
                table: "users",
                sql: "\"Role\" IN ('director','staff','parent')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_users_role",
                schema: "tenant_template",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "tenant_template",
                table: "users");
        }
    }
}
