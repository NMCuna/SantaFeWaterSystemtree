using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 23)
        INSERT INTO Permissions (Id, Description, Name) VALUES (23, 'Permission to view billing records', 'ViewBilling');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 24)
        INSERT INTO Permissions (Id, Description, Name) VALUES (24, 'Permission to edit billing records', 'EditBilling');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 25)
        INSERT INTO Permissions (Id, Description, Name) VALUES (25, 'Permission to delete billing records', 'DeleteBilling');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 26)
        INSERT INTO Permissions (Id, Description, Name) VALUES (26, 'Permission to send billing notifications', 'NotifyBilling');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 27)
        INSERT INTO Permissions (Id, Description, Name) VALUES (27, 'Permission to view penalty history logs', 'ViewPenaltyLog');
    ");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 27);
        }
    }
}
