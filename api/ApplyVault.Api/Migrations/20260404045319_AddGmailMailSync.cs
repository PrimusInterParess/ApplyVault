using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGmailMailSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastStatusEmailFrom",
                table: "ScrapeResults",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastStatusEmailReceivedAt",
                table: "ScrapeResults",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastStatusEmailSubject",
                table: "ScrapeResults",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastStatusKind",
                table: "ScrapeResults",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastStatusSource",
                table: "ScrapeResults",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastStatusUpdatedAt",
                table: "ScrapeResults",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastHistoryId",
                table: "ConnectedAccounts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSyncError",
                table: "ConnectedAccounts",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                table: "ConnectedAccounts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncStatus",
                table: "ConnectedAccounts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastStatusEmailFrom",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "LastStatusEmailReceivedAt",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "LastStatusEmailSubject",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "LastStatusKind",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "LastStatusSource",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "LastStatusUpdatedAt",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "LastHistoryId",
                table: "ConnectedAccounts");

            migrationBuilder.DropColumn(
                name: "LastSyncError",
                table: "ConnectedAccounts");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "ConnectedAccounts");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "ConnectedAccounts");
        }
    }
}
