using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20260804123100_AddInterviewPrepRealInterviewerState")]
    public partial class AddInterviewPrepRealInterviewerState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgendaJson",
                table: "InterviewPrepSessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "CurrentAgendaStep",
                table: "InterviewPrepSessions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "opening");

            migrationBuilder.AddColumn<string>(
                name: "InterviewerMemoryJson",
                table: "InterviewPrepSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterviewerProfile",
                table: "InterviewPrepSessions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "hiring_manager");

            migrationBuilder.AddColumn<string>(
                name: "LatestInterviewMove",
                table: "InterviewPrepSessions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TurnStateJson",
                table: "InterviewPrepSessionMessages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgendaJson",
                table: "InterviewPrepSessions");

            migrationBuilder.DropColumn(
                name: "CurrentAgendaStep",
                table: "InterviewPrepSessions");

            migrationBuilder.DropColumn(
                name: "InterviewerMemoryJson",
                table: "InterviewPrepSessions");

            migrationBuilder.DropColumn(
                name: "InterviewerProfile",
                table: "InterviewPrepSessions");

            migrationBuilder.DropColumn(
                name: "LatestInterviewMove",
                table: "InterviewPrepSessions");

            migrationBuilder.DropColumn(
                name: "TurnStateJson",
                table: "InterviewPrepSessionMessages");
        }
    }
}
