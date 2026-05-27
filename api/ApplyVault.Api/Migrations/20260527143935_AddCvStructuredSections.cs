using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCvStructuredSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MergeSortOrder",
                table: "UserCvProjectSummaries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "IncludeInMerge",
                table: "UserCvProjectSummaries",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StructuredImportedAt",
                table: "UserCvDocuments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserCvSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserCvDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Heading = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SectionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCvSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCvSections_UserCvDocuments_UserCvDocumentId",
                        column: x => x.UserCvDocumentId,
                        principalTable: "UserCvDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCvEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DateRange = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BulletsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechStack = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceSummaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCvEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCvEntries_UserCvProjectSummaries_SourceSummaryId",
                        column: x => x.SourceSummaryId,
                        principalTable: "UserCvProjectSummaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_UserCvEntries_UserCvSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "UserCvSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCvEntries_SectionId_SortOrder",
                table: "UserCvEntries",
                columns: new[] { "SectionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCvEntries_SourceSummaryId",
                table: "UserCvEntries",
                column: "SourceSummaryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCvSections_UserCvDocumentId_SortOrder",
                table: "UserCvSections",
                columns: new[] { "UserCvDocumentId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCvEntries");

            migrationBuilder.DropTable(
                name: "UserCvSections");

            migrationBuilder.DropColumn(
                name: "StructuredImportedAt",
                table: "UserCvDocuments");

            migrationBuilder.AlterColumn<int>(
                name: "MergeSortOrder",
                table: "UserCvProjectSummaries",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<bool>(
                name: "IncludeInMerge",
                table: "UserCvProjectSummaries",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);
        }
    }
}
