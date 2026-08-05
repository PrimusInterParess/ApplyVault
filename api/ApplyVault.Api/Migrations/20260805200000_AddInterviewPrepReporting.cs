using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20260805200000_AddInterviewPrepReporting")]
    public partial class AddInterviewPrepReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CandidateReportJson",
                table: "InterviewPrepSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StageAssessmentsJson",
                table: "InterviewPrepSessions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CandidateReportJson", table: "InterviewPrepSessions");
            migrationBuilder.DropColumn(name: "StageAssessmentsJson", table: "InterviewPrepSessions");
        }
    }
}
