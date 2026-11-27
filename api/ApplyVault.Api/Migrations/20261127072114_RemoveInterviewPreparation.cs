using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInterviewPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewPrepAnswerRetries");

            migrationBuilder.DropTable(
                name: "InterviewPrepCompetencyCoverages");

            migrationBuilder.DropTable(
                name: "InterviewPrepEvidenceItems");

            migrationBuilder.DropTable(
                name: "InterviewPrepQuestionAttempts");

            migrationBuilder.DropTable(
                name: "InterviewPrepStudyBriefs");

            migrationBuilder.DropTable(
                name: "InterviewPrepTurns");

            migrationBuilder.DropTable(
                name: "PreparationJobs");

            migrationBuilder.DropTable(
                name: "InterviewPrepStages");

            migrationBuilder.DropTable(
                name: "InterviewPrepSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewPrepSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CandidateReportJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CatalogVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CvDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CvSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExperienceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    InteractionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    JobSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Market = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PanelDebriefJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Persona = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PlanJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreparedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RuntimeStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScrapeResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StageAssessmentsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPrepSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPrepSessions_ScrapeResults_ScrapeResultId",
                        column: x => x.ScrapeResultId,
                        principalTable: "ScrapeResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InterviewPrepSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterviewPrepStudyBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BodyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CvDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CvFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FocusNoteSnapshot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Market = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ScrapeResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAiFallback = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WasJobBound = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPrepStudyBriefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPrepStudyBriefs_ScrapeResults_ScrapeResultId",
                        column: x => x.ScrapeResultId,
                        principalTable: "ScrapeResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InterviewPrepStudyBriefs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreparationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActiveKey = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DocumentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HelpJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InputFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    PackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuestionId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparationJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreparationJobs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterviewPrepAnswerRetries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachingFeedbackJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComparisonJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InterviewerTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalAnswerText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalAssessmentJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevisedAnswerText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevisedAssessmentJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "InterviewPrepCompetencyCoverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CompetencyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CoverageState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EvidenceCount = table.Column<int>(type: "int", nullable: false),
                    LastProgressClass = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
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
                    Claim = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompetencyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EvidenceQuote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Polarity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Strength = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
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
                    AssessmentJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssessmentStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CandidateTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompetencyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IntentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InterviewerTurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProgressClass = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Score = table.Column<int>(type: "int", nullable: true),
                    TargetEvidenceKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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

            migrationBuilder.CreateTable(
                name: "InterviewPrepStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PlanJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    StageType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPrepStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPrepStages_InterviewPrepSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InterviewPrepSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterviewPrepTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClientTurnId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CompetencyTag = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IntentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    QuestionSignature = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    TargetEvidenceKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPrepTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPrepTurns_InterviewPrepSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InterviewPrepSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterviewPrepTurns_InterviewPrepStages_StageId",
                        column: x => x.StageId,
                        principalTable: "InterviewPrepStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepSessions_ScrapeResultId",
                table: "InterviewPrepSessions",
                column: "ScrapeResultId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepSessions_UserId_IdempotencyKey",
                table: "InterviewPrepSessions",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepSessions_UserId_UpdatedAt",
                table: "InterviewPrepSessions",
                columns: new[] { "UserId", "UpdatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepStages_SessionId_SortOrder",
                table: "InterviewPrepStages",
                columns: new[] { "SessionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepStudyBriefs_ScrapeResultId",
                table: "InterviewPrepStudyBriefs",
                column: "ScrapeResultId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepStudyBriefs_UserId_CvOnly",
                table: "InterviewPrepStudyBriefs",
                column: "UserId",
                unique: true,
                filter: "[ScrapeResultId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepStudyBriefs_UserId_GeneratedAt",
                table: "InterviewPrepStudyBriefs",
                columns: new[] { "UserId", "GeneratedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepStudyBriefs_UserId_ScrapeResultId",
                table: "InterviewPrepStudyBriefs",
                columns: new[] { "UserId", "ScrapeResultId" },
                unique: true,
                filter: "[ScrapeResultId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepTurns_SessionId_ClientTurnId",
                table: "InterviewPrepTurns",
                columns: new[] { "SessionId", "ClientTurnId" },
                unique: true,
                filter: "[ClientTurnId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepTurns_SessionId_QuestionSignature",
                table: "InterviewPrepTurns",
                columns: new[] { "SessionId", "QuestionSignature" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepTurns_SessionId_Sequence",
                table: "InterviewPrepTurns",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepTurns_StageId",
                table: "InterviewPrepTurns",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationJobs_ActiveKey",
                table: "PreparationJobs",
                column: "ActiveKey",
                unique: true,
                filter: "[ActiveKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationJobs_Status_AvailableAt",
                table: "PreparationJobs",
                columns: new[] { "Status", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PreparationJobs_UserId_CreatedAt",
                table: "PreparationJobs",
                columns: new[] { "UserId", "CreatedAt" });
        }
    }
}
