using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class MakeDateOfBirthOptionalDropGpAddNrnHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GpName",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "GpPhone",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                schema: "tenant_template",
                table: "children",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<string>(
                name: "NrnHash",
                schema: "tenant_template",
                table: "children",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_children_NrnHash",
                schema: "tenant_template",
                table: "children",
                column: "NrnHash",
                unique: true,
                filter: "\"NrnHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_children_NrnHash",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.DropColumn(
                name: "NrnHash",
                schema: "tenant_template",
                table: "children");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                schema: "tenant_template",
                table: "children",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GpName",
                schema: "tenant_template",
                table: "children",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GpPhone",
                schema: "tenant_template",
                table: "children",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }
    }
}
