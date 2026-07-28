using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCvEntryFieldsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FieldsJson",
                table: "UserCvEntries",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FieldsJson",
                table: "UserCvEntries");
        }
    }
}
