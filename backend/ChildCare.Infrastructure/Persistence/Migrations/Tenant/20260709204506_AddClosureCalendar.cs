using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddClosureCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClosureConfirmedBy",
                schema: "tenant_template",
                table: "attendance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosureDayId",
                schema: "tenant_template",
                table: "attendance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriorStateJson",
                schema: "tenant_template",
                table: "attendance_records",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "kdv_closure_days",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClosureType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NotifyParents = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NotificationSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AttendanceGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttendanceGeneratedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kdv_closure_days", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kdv_closure_days_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_kdv_closure_days_users_AttendanceGeneratedBy",
                        column: x => x.AttendanceGeneratedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kdv_closure_days_users_CancelledBy",
                        column: x => x.CancelledBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kdv_closure_days_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kdv_closure_days_users_PublishedBy",
                        column: x => x.PublishedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kdv_closure_days_users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parent_closure_messages",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosureDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TitleKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BodyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ArgumentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parent_closure_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parent_closure_messages_contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "tenant_template",
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parent_closure_messages_kdv_closure_days_ClosureDayId",
                        column: x => x.ClosureDayId,
                        principalSchema: "tenant_template",
                        principalTable: "kdv_closure_days",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "closure_notification_deliveries",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosureDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PushToken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PushStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_closure_notification_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_closure_notification_deliveries_contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "tenant_template",
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_closure_notification_deliveries_kdv_closure_days_ClosureDay~",
                        column: x => x.ClosureDayId,
                        principalSchema: "tenant_template",
                        principalTable: "kdv_closure_days",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_closure_notification_deliveries_parent_closure_messages_Mes~",
                        column: x => x.MessageId,
                        principalSchema: "tenant_template",
                        principalTable: "parent_closure_messages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_ClosureDayId",
                schema: "tenant_template",
                table: "attendance_records",
                column: "ClosureDayId");

            migrationBuilder.CreateIndex(
                name: "IX_closure_notification_deliveries_ClosureDayId_ContactId_Kind",
                schema: "tenant_template",
                table: "closure_notification_deliveries",
                columns: new[] { "ClosureDayId", "ContactId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_closure_notification_deliveries_ContactId",
                schema: "tenant_template",
                table: "closure_notification_deliveries",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_closure_notification_deliveries_MessageId",
                schema: "tenant_template",
                table: "closure_notification_deliveries",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_kdv_closure_days_AttendanceGeneratedBy",
                schema: "tenant_template",
                table: "kdv_closure_days",
                column: "AttendanceGeneratedBy");

            migrationBuilder.CreateIndex(
                name: "IX_kdv_closure_days_CancelledBy",
                schema: "tenant_template",
                table: "kdv_closure_days",
                column: "CancelledBy");

            migrationBuilder.CreateIndex(
                name: "IX_kdv_closure_days_CreatedBy",
                schema: "tenant_template",
                table: "kdv_closure_days",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_kdv_closure_days_LocationId_Date",
                schema: "tenant_template",
                table: "kdv_closure_days",
                columns: new[] { "LocationId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kdv_closure_days_LocationId_Status_Date",
                schema: "tenant_template",
                table: "kdv_closure_days",
                columns: new[] { "LocationId", "Status", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_kdv_closure_days_PublishedBy",
                schema: "tenant_template",
                table: "kdv_closure_days",
                column: "PublishedBy");

            migrationBuilder.CreateIndex(
                name: "IX_kdv_closure_days_UpdatedBy",
                schema: "tenant_template",
                table: "kdv_closure_days",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_parent_closure_messages_ClosureDayId_ContactId_Kind",
                schema: "tenant_template",
                table: "parent_closure_messages",
                columns: new[] { "ClosureDayId", "ContactId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parent_closure_messages_ContactId",
                schema: "tenant_template",
                table: "parent_closure_messages",
                column: "ContactId");

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_records_kdv_closure_days_ClosureDayId",
                schema: "tenant_template",
                table: "attendance_records",
                column: "ClosureDayId",
                principalSchema: "tenant_template",
                principalTable: "kdv_closure_days",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attendance_records_kdv_closure_days_ClosureDayId",
                schema: "tenant_template",
                table: "attendance_records");

            migrationBuilder.DropTable(
                name: "closure_notification_deliveries",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "parent_closure_messages",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "kdv_closure_days",
                schema: "tenant_template");

            migrationBuilder.DropIndex(
                name: "IX_attendance_records_ClosureDayId",
                schema: "tenant_template",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "ClosureConfirmedBy",
                schema: "tenant_template",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "ClosureDayId",
                schema: "tenant_template",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "PriorStateJson",
                schema: "tenant_template",
                table: "attendance_records");
        }
    }
}
