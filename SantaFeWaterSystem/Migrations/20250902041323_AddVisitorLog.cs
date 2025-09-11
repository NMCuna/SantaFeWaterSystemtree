using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitorLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisitorLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IpAddress = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VisitDateLocal = table.Column<DateTime>(type: "date", nullable: false),
                    VisitedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorLogs_VisitDateLocal_IpAddress",
                table: "VisitorLogs",
                columns: new[] { "VisitDateLocal", "IpAddress" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitorLogs");
        }
    }
}
