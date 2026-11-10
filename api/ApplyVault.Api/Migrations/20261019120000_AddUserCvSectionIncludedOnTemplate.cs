using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplyVault.Api.Migrations
{
    /// <summary>
    /// Section visibility on the template. Existing rows stay included (default 1).
    /// </summary>
    [DbContext(typeof(ApplyVaultDbContext))]
    [Migration("20261019120000_AddUserCvSectionIncludedOnTemplate")]
    public partial class AddUserCvSectionIncludedOnTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludedOnTemplate",
                table: "UserCvSections",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludedOnTemplate",
                table: "UserCvSections");
        }
    }
}
