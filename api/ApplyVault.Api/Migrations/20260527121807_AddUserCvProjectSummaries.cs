using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCvProjectSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserCvProjectSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalRepoId = table.Column<long>(type: "bigint", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    HtmlUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PrimaryLanguage = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Topics = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CvTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CvSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CvBullets = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechStack = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCvProjectSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCvProjectSummaries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCvProjectSummaries_UserId_ExternalRepoId",
                table: "UserCvProjectSummaries",
                columns: new[] { "UserId", "ExternalRepoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCvProjectSummaries");
        }
    }
}
