using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20260805143000_AddInterviewPrepV2")]
    public partial class AddInterviewPrepV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewPrepSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScrapeResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Mode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Persona = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Market = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExperienceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InteractionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CvDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CvSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JobSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CatalogVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PreparedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                name: "InterviewPrepStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    StageType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PlanJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuestionSignature = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CompetencyTag = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClientTurnId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewPrepTurns");

            migrationBuilder.DropTable(
                name: "InterviewPrepStages");

            migrationBuilder.DropTable(
                name: "InterviewPrepSessions");
        }
    }
}
