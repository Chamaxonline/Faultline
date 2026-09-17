using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faultline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueAssignee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserId",
                table: "Issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Issues_AssignedToUserId",
                table: "Issues",
                column: "AssignedToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Users_AssignedToUserId",
                table: "Issues",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Users_AssignedToUserId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_AssignedToUserId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "Issues");
        }
    }
}
