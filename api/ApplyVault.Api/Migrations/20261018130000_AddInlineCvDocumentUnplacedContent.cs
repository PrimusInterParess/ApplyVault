using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <summary>
    /// Stores importer leftovers on the CV document as a JSON string list (unplaced content drawer).
    /// </summary>
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20261018130000_AddInlineCvDocumentUnplacedContent")]
    public partial class AddInlineCvDocumentUnplacedContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnplacedContentJson",
                table: "UserCvDocuments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnplacedContentJson",
                table: "UserCvDocuments");
        }
    }
}
