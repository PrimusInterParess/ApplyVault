using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapeResultRejectionFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRejected",
                table: "ScrapeResults",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRejected",
                table: "ScrapeResults");
        }
    }
}
