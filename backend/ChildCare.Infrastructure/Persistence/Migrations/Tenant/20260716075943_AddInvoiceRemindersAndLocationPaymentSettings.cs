using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddInvoiceRemindersAndLocationPaymentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentReminderCadenceDays",
                schema: "tenant_template",
                table: "locations",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "PaymentReminderDelayDays",
                schema: "tenant_template",
                table: "locations",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<bool>(
                name: "PaymentRemindersEnabled",
                schema: "tenant_template",
                table: "locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReminderSentAt",
                schema: "tenant_template",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderCount",
                schema: "tenant_template",
                table: "invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentReminderCadenceDays",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "PaymentReminderDelayDays",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "PaymentRemindersEnabled",
                schema: "tenant_template",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "LastReminderSentAt",
                schema: "tenant_template",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "ReminderCount",
                schema: "tenant_template",
                table: "invoices");
        }
    }
}
