using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultAdminToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAccessSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoginViewToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAccessSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AdminAccessSettings",
                columns: new[] { "Id", "LoginViewToken" },
                values: new object[] { 1, "wako-kabalo-ganiiiii" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAccessSettings");
        }
    }
}
