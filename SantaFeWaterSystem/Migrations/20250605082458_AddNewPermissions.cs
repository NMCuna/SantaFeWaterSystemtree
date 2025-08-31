using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddNewPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 10)
        INSERT INTO Permissions (Id, Description, Name) VALUES (10, 'Permission to register new admins', 'RegisterAdmin');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 11)
        INSERT INTO Permissions (Id, Description, Name) VALUES (11, 'Permission to register new users', 'RegisterUser');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 12)
        INSERT INTO Permissions (Id, Description, Name) VALUES (12, 'Permission to manage QR codes', 'ManageQRCodes');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 13)
        INSERT INTO Permissions (Id, Description, Name) VALUES (13, 'Permission to manage rates', 'ManageRate');
    ");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 13);
        }
    }
}
