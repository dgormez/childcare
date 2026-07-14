using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddMonthlyMenuAndMealPreferenceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meal_preference_change_requests",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    NewTexture = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NewDietaryType = table.Column<List<string>>(type: "text[]", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_preference_change_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meal_preference_change_requests_children_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "tenant_template",
                        principalTable: "children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meal_preference_change_requests_users_DecidedBy",
                        column: x => x.DecidedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "monthly_menus",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_menus", x => x.Id);
                    table.CheckConstraint("ck_monthly_menus_month", "\"Month\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_monthly_menus_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "tenant_template",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_monthly_menus_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "tenant_template",
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "monthly_menu_days",
                schema: "tenant_template",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Soup = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MainCourse = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Dessert = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_menu_days", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monthly_menu_days_monthly_menus_MenuId",
                        column: x => x.MenuId,
                        principalSchema: "tenant_template",
                        principalTable: "monthly_menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_meal_preference_change_requests_ChildId_Status",
                schema: "tenant_template",
                table: "meal_preference_change_requests",
                columns: new[] { "ChildId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_meal_preference_change_requests_DecidedBy",
                schema: "tenant_template",
                table: "meal_preference_change_requests",
                column: "DecidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_menu_days_MenuId_MenuDate",
                schema: "tenant_template",
                table: "monthly_menu_days",
                columns: new[] { "MenuId", "MenuDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_menus_CreatedBy",
                schema: "tenant_template",
                table: "monthly_menus",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_menus_LocationId_Year_Month",
                schema: "tenant_template",
                table: "monthly_menus",
                columns: new[] { "LocationId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meal_preference_change_requests",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "monthly_menu_days",
                schema: "tenant_template");

            migrationBuilder.DropTable(
                name: "monthly_menus",
                schema: "tenant_template");
        }
    }
}
