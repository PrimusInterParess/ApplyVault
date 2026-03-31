using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePayloadJsonWithStructuredColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectedPageType",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExtractedAt",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HiringManagerName",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobDescription",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionSummary",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceHostname",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TextLength",
                table: "ScrapeResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ScrapeResultContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScrapeResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapeResultContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrapeResultContacts_ScrapeResults_ScrapeResultId",
                        column: x => x.ScrapeResultId,
                        principalTable: "ScrapeResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapeResultContacts_ScrapeResultId",
                table: "ScrapeResultContacts",
                column: "ScrapeResultId");

            migrationBuilder.Sql(
                """
                UPDATE [ScrapeResults]
                SET
                    [Title] = COALESCE([parsed].[Title], ''),
                    [Url] = COALESCE([parsed].[Url], ''),
                    [Text] = COALESCE([parsed].[Text], ''),
                    [TextLength] = COALESCE([parsed].[TextLength], 0),
                    [ExtractedAt] = COALESCE([parsed].[ExtractedAt], ''),
                    [SourceHostname] = COALESCE([parsed].[SourceHostname], ''),
                    [DetectedPageType] = COALESCE([parsed].[DetectedPageType], ''),
                    [JobTitle] = [parsed].[JobTitle],
                    [CompanyName] = [parsed].[CompanyName],
                    [Location] = [parsed].[Location],
                    [JobDescription] = [parsed].[JobDescription],
                    [PositionSummary] = [parsed].[PositionSummary],
                    [HiringManagerName] = [parsed].[HiringManagerName]
                FROM [ScrapeResults]
                CROSS APPLY OPENJSON([PayloadJson])
                WITH (
                    [Title] nvarchar(max) '$.title',
                    [Url] nvarchar(max) '$.url',
                    [Text] nvarchar(max) '$.text',
                    [TextLength] int '$.textLength',
                    [ExtractedAt] nvarchar(max) '$.extractedAt',
                    [SourceHostname] nvarchar(max) '$.jobDetails.sourceHostname',
                    [DetectedPageType] nvarchar(max) '$.jobDetails.detectedPageType',
                    [JobTitle] nvarchar(max) '$.jobDetails.jobTitle',
                    [CompanyName] nvarchar(max) '$.jobDetails.companyName',
                    [Location] nvarchar(max) '$.jobDetails.location',
                    [JobDescription] nvarchar(max) '$.jobDetails.jobDescription',
                    [PositionSummary] nvarchar(max) '$.jobDetails.positionSummary',
                    [HiringManagerName] nvarchar(max) '$.jobDetails.hiringManagerName'
                ) AS [parsed];
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO [ScrapeResultContacts] ([ScrapeResultId], [Type], [Value], [Label])
                SELECT
                    [result].[Id],
                    [contact].[Type],
                    [contact].[Value],
                    [contact].[Label]
                FROM [ScrapeResults] AS [result]
                CROSS APPLY OPENJSON([result].[PayloadJson], '$.jobDetails.hiringManagerContacts')
                WITH (
                    [Type] nvarchar(max) '$.type',
                    [Value] nvarchar(max) '$.value',
                    [Label] nvarchar(max) '$.label'
                ) AS [contact]
                WHERE [contact].[Type] IS NOT NULL
                    AND [contact].[Value] IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "PayloadJson",
                table: "ScrapeResults");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayloadJson",
                table: "ScrapeResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE [result]
                SET [PayloadJson] =
                (
                    SELECT
                        [result].[Title] AS [title],
                        [result].[Url] AS [url],
                        [result].[Text] AS [text],
                        [result].[TextLength] AS [textLength],
                        [result].[ExtractedAt] AS [extractedAt],
                        JSON_QUERY(
                            (
                                SELECT
                                    [result].[SourceHostname] AS [sourceHostname],
                                    [result].[DetectedPageType] AS [detectedPageType],
                                    [result].[JobTitle] AS [jobTitle],
                                    [result].[CompanyName] AS [companyName],
                                    [result].[Location] AS [location],
                                    [result].[JobDescription] AS [jobDescription],
                                    [result].[PositionSummary] AS [positionSummary],
                                    [result].[HiringManagerName] AS [hiringManagerName],
                                    JSON_QUERY(COALESCE([contacts].[HiringManagerContactsJson], '[]')) AS [hiringManagerContacts]
                                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                            )
                        ) AS [jobDetails]
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                )
                FROM [ScrapeResults] AS [result]
                OUTER APPLY
                (
                    SELECT
                        (
                            SELECT
                                [contact].[Type] AS [type],
                                [contact].[Value] AS [value],
                                [contact].[Label] AS [label]
                            FROM [ScrapeResultContacts] AS [contact]
                            WHERE [contact].[ScrapeResultId] = [result].[Id]
                            ORDER BY [contact].[Id]
                            FOR JSON PATH
                        ) AS [HiringManagerContactsJson]
                ) AS [contacts];
                """);

            migrationBuilder.DropTable(
                name: "ScrapeResultContacts");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "DetectedPageType",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "ExtractedAt",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "HiringManagerName",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "JobDescription",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "PositionSummary",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "SourceHostname",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "TextLength",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ScrapeResults");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "ScrapeResults");
        }
    }
}
