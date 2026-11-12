using System;
using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20261110163000_AddScrapeResultCoverLetter")]
    public partial class AddScrapeResultCoverLetter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverLetter",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CoverLetterUpdatedAt",
                table: "ScrapeResults",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverLetter",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "CoverLetterUpdatedAt",
                table: "ScrapeResults");
        }
    }
}
