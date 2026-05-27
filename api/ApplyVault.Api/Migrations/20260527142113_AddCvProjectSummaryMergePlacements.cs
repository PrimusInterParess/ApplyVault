using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCvProjectSummaryMergePlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeInMerge",
                table: "UserCvProjectSummaries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MergeSectionHeading",
                table: "UserCvProjectSummaries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MergeSortOrder",
                table: "UserCvProjectSummaries",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeInMerge",
                table: "UserCvProjectSummaries");

            migrationBuilder.DropColumn(
                name: "MergeSectionHeading",
                table: "UserCvProjectSummaries");

            migrationBuilder.DropColumn(
                name: "MergeSortOrder",
                table: "UserCvProjectSummaries");
        }
    }
}
