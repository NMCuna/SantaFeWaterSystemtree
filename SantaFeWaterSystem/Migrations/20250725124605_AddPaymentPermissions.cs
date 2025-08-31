using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 28)
        INSERT INTO Permissions (Id, Description, Name) VALUES (28, 'Permission to view payment records', 'ViewPayment');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 29)
        INSERT INTO Permissions (Id, Description, Name) VALUES (29, 'Permission to edit payment records', 'EditPayment');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 30)
        INSERT INTO Permissions (Id, Description, Name) VALUES (30, 'Permission to delete payment records', 'DeletePayment');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 31)
        INSERT INTO Permissions (Id, Description, Name) VALUES (31, 'Permission to verify payment records', 'VerifyPayment');
    ");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 31);
        }
    }
}
