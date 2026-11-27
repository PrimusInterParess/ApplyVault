using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRequirementsExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequirementsAttempts",
                table: "ScrapeResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequirementsJson",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequirementsLeaseExpiresUtc",
                table: "ScrapeResults",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequirementsLeaseId",
                table: "ScrapeResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequirementsNextAttemptUtc",
                table: "ScrapeResults",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RequirementsRevision",
                table: "ScrapeResults",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "RequirementsSourceHash",
                table: "ScrapeResults",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequirementsStatus",
                table: "ScrapeResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ScrapeResults_IsDeleted_RequirementsStatus_RequirementsNextAttemptUtc",
                table: "ScrapeResults",
                columns: new[] { "IsDeleted", "RequirementsStatus", "RequirementsNextAttemptUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScrapeResults_IsDeleted_RequirementsStatus_RequirementsNextAttemptUtc",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "RequirementsAttempts",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "RequirementsJson",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "RequirementsLeaseExpiresUtc",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "RequirementsLeaseId",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "RequirementsNextAttemptUtc",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "RequirementsRevision",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "RequirementsSourceHash",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "RequirementsStatus",
                table: "ScrapeResults");
        }
    }
}
