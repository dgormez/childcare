using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddEmailCommunications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DigestUnsubscribedAt",
                schema: "tenant_template",
                table: "contacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bulk_email_sends",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    AttachmentObjectPath = table.Column<string>(type: "text", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "text", nullable: true),
                    AttachmentContentType = table.Column<string>(type: "text", nullable: true),
                    SentByTenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bulk_email_sends", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bulk_email_sends_groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "tenant_template",
                        principalTable: "groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_bulk_email_sends_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bulk_email_sends_users_SentByTenantUserId",
                        column: x => x.SentByTenantUserId,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bulk_email_recipients",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BulkEmailSendId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bulk_email_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bulk_email_recipients_bulk_email_sends_BulkEmailSendId",
                        column: x => x.BulkEmailSendId,
                        principalSchema: "tenant_template",
                        principalTable: "bulk_email_sends",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bulk_email_recipients_contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "tenant_template",
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bulk_email_recipients_BulkEmailSendId",
                schema: "tenant_template",
                table: "bulk_email_recipients",
                column: "BulkEmailSendId");

            migrationBuilder.CreateIndex(
                name: "IX_bulk_email_recipients_ContactId",
                schema: "tenant_template",
                table: "bulk_email_recipients",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_bulk_email_sends_GroupId",
                schema: "tenant_template",
                table: "bulk_email_sends",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_bulk_email_sends_LocationId",
                schema: "tenant_template",
                table: "bulk_email_sends",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_bulk_email_sends_SentAt",
                schema: "tenant_template",
                table: "bulk_email_sends",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_bulk_email_sends_SentByTenantUserId",
                schema: "tenant_template",
                table: "bulk_email_sends",
                column: "SentByTenantUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bulk_email_recipients",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "bulk_email_sends",
                schema: "tenant_template");

            migrationBuilder.DropColumn(
                name: "DigestUnsubscribedAt",
                schema: "tenant_template",
                table: "contacts");
        }
    }
}
