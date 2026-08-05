using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20260805160000_AddInterviewPrepBriefPlanJson")]
    public partial class AddInterviewPrepBriefPlanJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BriefJson",
                table: "InterviewPrepSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanJson",
                table: "InterviewPrepSessions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BriefJson",
                table: "InterviewPrepSessions");

            migrationBuilder.DropColumn(
                name: "PlanJson",
                table: "InterviewPrepSessions");
        }
    }
}
