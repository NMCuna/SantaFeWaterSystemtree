using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminResetTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminResetTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Day = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminResetTokens", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AdminResetTokens",
                columns: new[] { "Id", "Day", "Token" },
                values: new object[,]
                {
                    { 1, "Monday", "TokenMon" },
                    { 2, "Tuesday", "TokenTue" },
                    { 3, "Wednesday", "TokenWed" },
                    { 4, "Thursday", "TokenThu" },
                    { 5, "Friday", "TokenFri" },
                    { 6, "Saturday", "TokenSat" },
                    { 7, "Sunday", "TokenSun" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminResetTokens");
        }
    }
}
