using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class FixPasswordHistoryRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "PasswordHistories",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordHistories_UserId",
                table: "PasswordHistories",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PasswordHistories_Users_UserId",
                table: "PasswordHistories",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PasswordHistories_Users_UserId",
                table: "PasswordHistories");

            migrationBuilder.DropIndex(
                name: "IX_PasswordHistories_UserId",
                table: "PasswordHistories");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PasswordHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
