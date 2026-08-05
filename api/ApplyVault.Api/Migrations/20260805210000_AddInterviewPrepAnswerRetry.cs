using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20260805210000_AddInterviewPrepAnswerRetry")]
    public partial class AddInterviewPrepAnswerRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewPrepAnswerRetries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewerTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalAnswerText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalAssessmentJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoachingFeedbackJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevisedAnswerText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevisedAssessmentJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComparisonJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPrepAnswerRetries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPrepAnswerRetries_InterviewPrepSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InterviewPrepSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepAnswerRetries_CandidateTurnId",
                table: "InterviewPrepAnswerRetries",
                column: "CandidateTurnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepAnswerRetries_SessionId",
                table: "InterviewPrepAnswerRetries",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewPrepAnswerRetries");
        }
    }
}
