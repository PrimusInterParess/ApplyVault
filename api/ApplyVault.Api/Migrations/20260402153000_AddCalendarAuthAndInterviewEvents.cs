using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarAuthAndInterviewEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ScrapeResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupabaseUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterviewEvents",
                columns: table => new
                {
                    ScrapeResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewEvents", x => x.ScrapeResultId);
                    table.ForeignKey(
                        name: "FK_InterviewEvents_ScrapeResults_ScrapeResultId",
                        column: x => x.ScrapeResultId,
                        principalTable: "ScrapeResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectedAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectedAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectedAccounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEventLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScrapeResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectedAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExternalEventId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalEventUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEventLinks_ConnectedAccounts_ConnectedAccountId",
                        column: x => x.ConnectedAccountId,
                        principalTable: "ConnectedAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarEventLinks_ScrapeResults_ScrapeResultId",
                        column: x => x.ScrapeResultId,
                        principalTable: "ScrapeResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapeResults_UserId",
                table: "ScrapeResults",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventLinks_ConnectedAccountId_ExternalEventId",
                table: "CalendarEventLinks",
                columns: new[] { "ConnectedAccountId", "ExternalEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventLinks_ScrapeResultId_ConnectedAccountId",
                table: "CalendarEventLinks",
                columns: new[] { "ScrapeResultId", "ConnectedAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_UserId_Provider_ProviderUserId",
                table: "ConnectedAccounts",
                columns: new[] { "UserId", "Provider", "ProviderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SupabaseUserId",
                table: "Users",
                column: "SupabaseUserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScrapeResults_Users_UserId",
                table: "ScrapeResults",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScrapeResults_Users_UserId",
                table: "ScrapeResults");

            migrationBuilder.DropTable(
                name: "CalendarEventLinks");

            migrationBuilder.DropTable(
                name: "InterviewEvents");

            migrationBuilder.DropTable(
                name: "ConnectedAccounts");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ScrapeResults_UserId",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ScrapeResults");
        }
    }
}
