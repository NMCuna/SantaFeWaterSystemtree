using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumerPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 20)
        INSERT INTO Permissions (Id, Description, Name) VALUES (20, 'Permission to view consumer details', 'ViewConsumer');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 21)
        INSERT INTO Permissions (Id, Description, Name) VALUES (21, 'Permission to edit consumer', 'EditConsumer');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 22)
        INSERT INTO Permissions (Id, Description, Name) VALUES (22, 'Permission to delete consumer', 'DeleteConsumer');
    ");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 22);
        }
    }
}
