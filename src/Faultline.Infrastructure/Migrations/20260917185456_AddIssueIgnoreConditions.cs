using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faultline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueIgnoreConditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IgnoreUntilCount",
                table: "Issues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IgnoreUntilDate",
                table: "Issues",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IgnoreUntilCount",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "IgnoreUntilDate",
                table: "Issues");
        }
    }
}
