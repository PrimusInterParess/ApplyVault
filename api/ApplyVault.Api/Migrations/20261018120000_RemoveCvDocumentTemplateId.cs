using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <summary>
    /// CV export collapsed to a single template; the per-document template choice is gone.
    /// </summary>
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20261018120000_RemoveCvDocumentTemplateId")]
    public partial class RemoveCvDocumentTemplateId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "UserCvDocuments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TemplateId",
                table: "UserCvDocuments",
                type: "int",
                nullable: false,
                defaultValue: 2);
        }
    }
}
