using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAppleIdPasswordResetAndPushToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppleId",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetExpiry",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpoPushToken",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AppleId",             table: "Users");
            migrationBuilder.DropColumn(name: "PasswordResetToken",  table: "Users");
            migrationBuilder.DropColumn(name: "PasswordResetExpiry", table: "Users");
            migrationBuilder.DropColumn(name: "ExpoPushToken",       table: "Users");
        }
    }
}
