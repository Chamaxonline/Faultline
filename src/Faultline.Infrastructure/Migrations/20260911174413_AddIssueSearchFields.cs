using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faultline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueSearchFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastEnvironment",
                table: "Issues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastRelease",
                table: "Issues",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastEnvironment",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "LastRelease",
                table: "Issues");
        }
    }
}
