using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20260805190000_AddInterviewPrepAdaptiveRuntime")]
    public partial class AddInterviewPrepAdaptiveRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConversationSummary",
                table: "InterviewPrepSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeStateJson",
                table: "InterviewPrepSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionType",
                table: "InterviewPrepTurns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntentId",
                table: "InterviewPrepTurns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetEvidenceKey",
                table: "InterviewPrepTurns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InterviewPrepCompetencyCoverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompetencyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CoverageState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastProgressClass = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    EvidenceCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPrepCompetencyCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPrepCompetencyCoverages_InterviewPrepSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InterviewPrepSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterviewPrepEvidenceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompetencyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Strength = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Claim = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    EvidenceQuote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Polarity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPrepEvidenceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPrepEvidenceItems_InterviewPrepSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InterviewPrepSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterviewPrepQuestionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewerTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CandidateTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IntentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CompetencyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TargetEvidenceKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProgressClass = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Score = table.Column<int>(type: "int", nullable: true),
                    AssessmentJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssessmentStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPrepQuestionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPrepQuestionAttempts_InterviewPrepSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InterviewPrepSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepCompetencyCoverages_SessionId_CompetencyId",
                table: "InterviewPrepCompetencyCoverages",
                columns: new[] { "SessionId", "CompetencyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepEvidenceItems_SessionId_CompetencyId",
                table: "InterviewPrepEvidenceItems",
                columns: new[] { "SessionId", "CompetencyId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepQuestionAttempts_SessionId_CandidateTurnId",
                table: "InterviewPrepQuestionAttempts",
                columns: new[] { "SessionId", "CandidateTurnId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InterviewPrepCompetencyCoverages");
            migrationBuilder.DropTable(name: "InterviewPrepEvidenceItems");
            migrationBuilder.DropTable(name: "InterviewPrepQuestionAttempts");

            migrationBuilder.DropColumn(name: "ConversationSummary", table: "InterviewPrepSessions");
            migrationBuilder.DropColumn(name: "RuntimeStateJson", table: "InterviewPrepSessions");
            migrationBuilder.DropColumn(name: "ActionType", table: "InterviewPrepTurns");
            migrationBuilder.DropColumn(name: "IntentId", table: "InterviewPrepTurns");
            migrationBuilder.DropColumn(name: "TargetEvidenceKey", table: "InterviewPrepTurns");
        }
    }
}
