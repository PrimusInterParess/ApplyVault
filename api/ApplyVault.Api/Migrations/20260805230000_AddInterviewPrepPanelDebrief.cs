using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20260805230000_AddInterviewPrepPanelDebrief")]
    public partial class AddInterviewPrepPanelDebrief : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PanelDebriefJson",
                table: "InterviewPrepSessions",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PanelDebriefJson", table: "InterviewPrepSessions");
        }
    }
}
