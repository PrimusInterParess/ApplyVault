using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20260806120000_AddInterviewPrepStudyBriefs")]
    public partial class AddInterviewPrepStudyBriefs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewPrepStudyBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScrapeResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Market = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FocusNoteSnapshot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BodyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CvFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CvDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    WasJobBound = table.Column<bool>(type: "bit", nullable: false),
                    UsedAiFallback = table.Column<bool>(type: "bit", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InterviewPrepStudyBriefs");
        }
    }
}
