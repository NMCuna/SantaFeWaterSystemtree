using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class SeedPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 1)
        INSERT INTO Permissions (Id, Description, Name) VALUES (1, 'Access to user management', 'ManageUsers');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 2)
        INSERT INTO Permissions (Id, Description, Name) VALUES (2, 'Access to consumer management', 'ManageConsumers');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 3)
        INSERT INTO Permissions (Id, Description, Name) VALUES (3, 'Access to billing management', 'ManageBilling');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 4)
        INSERT INTO Permissions (Id, Description, Name) VALUES (4, 'Access to payment management', 'ManagePayments');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 5)
        INSERT INTO Permissions (Id, Description, Name) VALUES (5, 'Access to disconnection management', 'ManageDisconnections');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 6)
        INSERT INTO Permissions (Id, Description, Name) VALUES (6, 'Access to reports', 'ViewReports');
    ");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 6);
        }
    }
}
