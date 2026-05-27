using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceScrapeResultUserOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE ScrapeResults
                SET IsDeleted = 1
                WHERE UserId IS NULL AND IsDeleted = 0;

                DELETE FROM ScrapeResults
                WHERE UserId IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ScrapeResults_Users_UserId",
                table: "ScrapeResults");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "ScrapeResults",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScrapeResults_Users_UserId",
                table: "ScrapeResults",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScrapeResults_Users_UserId",
                table: "ScrapeResults");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "ScrapeResults",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_ScrapeResults_Users_UserId",
                table: "ScrapeResults",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
