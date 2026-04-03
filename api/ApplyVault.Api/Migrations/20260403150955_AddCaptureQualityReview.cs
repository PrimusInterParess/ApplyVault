using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaptureQualityReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CaptureOverallConfidence",
                table: "ScrapeResults",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "CaptureReviewStatus",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "not_required");

            migrationBuilder.AddColumn<double>(
                name: "CompanyNameConfidence",
                table: "ScrapeResults",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "CompanyNameOverride",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyNameReviewReason",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "JobDescriptionConfidence",
                table: "ScrapeResults",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "JobDescriptionOverride",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobDescriptionReviewReason",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "JobTitleConfidence",
                table: "ScrapeResults",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "JobTitleOverride",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitleReviewReason",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationConfidence",
                table: "ScrapeResults",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "LocationOverride",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationReviewReason",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaptureOverallConfidence",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "CaptureReviewStatus",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "CompanyNameConfidence",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "CompanyNameOverride",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "CompanyNameReviewReason",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "JobDescriptionConfidence",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "JobDescriptionOverride",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "JobDescriptionReviewReason",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "JobTitleConfidence",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "JobTitleOverride",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "JobTitleReviewReason",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "LocationConfidence",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "LocationOverride",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "LocationReviewReason",
                table: "ScrapeResults");
        }
    }
}
