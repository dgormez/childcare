using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddParentCommunication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantUserId",
                schema: "tenant_template",
                table: "contacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "announcements",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    SentByTenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_announcements_groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "tenant_template",
                        principalTable: "groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_announcements_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_announcements_users_SentByTenantUserId",
                        column: x => x.SentByTenantUserId,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "message_threads",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_threads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_message_threads_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TitleKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BodyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ArgumentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_users_TenantUserId",
                        column: x => x.TenantUserId,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parent_invitations",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parent_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parent_invitations_contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "tenant_template",
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "announcement_recipients",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnnouncementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcement_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_announcement_recipients_announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalSchema: "tenant_template",
                        principalTable: "announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_announcement_recipients_contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "tenant_template",
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "message_thread_participants",
                schema: "tenant_template",
                columns: table => new
                {
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_thread_participants", x => new { x.ThreadId, x.TenantUserId });
                    table.ForeignKey(
                        name: "FK_message_thread_participants_message_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalSchema: "tenant_template",
                        principalTable: "message_threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_message_thread_participants_users_TenantUserId",
                        column: x => x.TenantUserId,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_messages_message_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalSchema: "tenant_template",
                        principalTable: "message_threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_messages_users_SenderId",
                        column: x => x.SenderId,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantUserId",
                schema: "tenant_template",
                table: "contacts",
                column: "TenantUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_announcement_recipients_AnnouncementId_ContactId",
                schema: "tenant_template",
                table: "announcement_recipients",
                columns: new[] { "AnnouncementId", "ContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_announcement_recipients_ContactId",
                schema: "tenant_template",
                table: "announcement_recipients",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_GroupId",
                schema: "tenant_template",
                table: "announcements",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_LocationId",
                schema: "tenant_template",
                table: "announcements",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_SentAt",
                schema: "tenant_template",
                table: "announcements",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_SentByTenantUserId",
                schema: "tenant_template",
                table: "announcements",
                column: "SentByTenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_message_thread_participants_TenantUserId",
                schema: "tenant_template",
                table: "message_thread_participants",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_message_threads_ChildId",
                schema: "tenant_template",
                table: "message_threads",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_message_threads_LastActivityAt",
                schema: "tenant_template",
                table: "message_threads",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_messages_SenderId",
                schema: "tenant_template",
                table: "messages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_messages_ThreadId_SentAt",
                schema: "tenant_template",
                table: "messages",
                columns: new[] { "ThreadId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_TenantUserId_CreatedAt",
                schema: "tenant_template",
                table: "notifications",
                columns: new[] { "TenantUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_parent_invitations_ContactId",
                schema: "tenant_template",
                table: "parent_invitations",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_parent_invitations_TokenHash",
                schema: "tenant_template",
                table: "parent_invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_contacts_users_TenantUserId",
                schema: "tenant_template",
                table: "contacts",
                column: "TenantUserId",
                principalSchema: "tenant_template",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contacts_users_TenantUserId",
                schema: "tenant_template",
                table: "contacts");

            migrationBuilder.DropTable(
                name: "announcement_recipients",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "message_thread_participants",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "messages",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "parent_invitations",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "announcements",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "message_threads",
                schema: "tenant_template");

            migrationBuilder.DropIndex(
                name: "IX_contacts_TenantUserId",
                schema: "tenant_template",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "TenantUserId",
                schema: "tenant_template",
                table: "contacts");
        }
    }
}
