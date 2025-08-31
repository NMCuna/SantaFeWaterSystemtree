using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class EnableCascadeDeleteUserConsumer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Consumers_Users_UserId",
                table: "Consumers");

            migrationBuilder.AddForeignKey(
                name: "FK_Consumers_Users_UserId",
                table: "Consumers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Consumers_Users_UserId",
                table: "Consumers");

            migrationBuilder.AddForeignKey(
                name: "FK_Consumers_Users_UserId",
                table: "Consumers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
