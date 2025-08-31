using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddMorePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 7)
        INSERT INTO Permissions (Id, Description, Name) VALUES (7, 'Access to notifications management', 'ManageNotifications');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 8)
        INSERT INTO Permissions (Id, Description, Name) VALUES (8, 'Access to support management', 'ManageSupport');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 9)
        INSERT INTO Permissions (Id, Description, Name) VALUES (9, 'Access to feedback management', 'ManageFeedback');
    ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 9);
        }
    }
}
