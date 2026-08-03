using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewPrepSessions : Migration
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
                    Mode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LanguageMix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    HiringMarket = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScrapeResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InferenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LatestScorecardJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LatestOverallScore = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                name: "InterviewPrepSessionMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ScorecardJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FollowUpsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DebriefBulletsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelAnswer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InferenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPrepSessionMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPrepSessionMessages_InterviewPrepSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InterviewPrepSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepSessions_ScrapeResultId",
                table: "InterviewPrepSessions",
                column: "ScrapeResultId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepSessions_UserId_UpdatedAt",
                table: "InterviewPrepSessions",
                columns: new[] { "UserId", "UpdatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPrepSessionMessages_SessionId_Sequence",
                table: "InterviewPrepSessionMessages",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewPrepSessionMessages");

            migrationBuilder.DropTable(
                name: "InterviewPrepSessions");
        }
    }
}
