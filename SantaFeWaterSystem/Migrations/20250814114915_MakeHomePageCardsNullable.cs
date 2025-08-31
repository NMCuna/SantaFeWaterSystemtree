using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class MakeHomePageCardsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HomePageContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subtitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card1Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card1Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card1Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card2Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card2Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card2Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card3Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card3Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card3Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card4Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card4Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card4Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card5Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card5Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Card5Icon = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomePageContents", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomePageContents");
        }
    }
}
