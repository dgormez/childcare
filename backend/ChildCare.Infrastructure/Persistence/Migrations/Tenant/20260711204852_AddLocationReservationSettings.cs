using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddLocationReservationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReservationAbsencesMode",
                schema: "tenant_template",
                table: "locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "approval");

            migrationBuilder.AddColumn<string>(
                name: "ReservationExtrasMode",
                schema: "tenant_template",
                table: "locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "approval");

            migrationBuilder.AddColumn<int>(
                name: "ReservationNoticeHours",
                schema: "tenant_template",
                table: "locations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReservationSwapsMode",
                schema: "tenant_template",
                table: "locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "disabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservationAbsencesMode",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "ReservationExtrasMode",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "ReservationNoticeHours",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "ReservationSwapsMode",
                schema: "tenant_template",
                table: "locations");
        }
    }
}
