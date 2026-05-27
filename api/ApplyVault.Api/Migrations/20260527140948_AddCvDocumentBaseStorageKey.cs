using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCvDocumentBaseStorageKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseStorageKey",
                table: "UserCvDocuments",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE UserCvDocuments
                SET BaseStorageKey = StorageKey
                WHERE BaseStorageKey IS NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseStorageKey",
                table: "UserCvDocuments");
        }
    }
}
