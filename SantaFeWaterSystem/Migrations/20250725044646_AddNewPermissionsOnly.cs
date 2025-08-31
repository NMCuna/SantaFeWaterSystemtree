using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddNewPermissionsOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 14)
        INSERT INTO Permissions (Id, Description, Name) VALUES (14, 'Permission to edit user details', 'EditUser');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 15)
        INSERT INTO Permissions (Id, Description, Name) VALUES (15, 'Permission to reset user password', 'ResetPassword');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 16)
        INSERT INTO Permissions (Id, Description, Name) VALUES (16, 'Permission to delete a user', 'DeleteUser');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 17)
        INSERT INTO Permissions (Id, Description, Name) VALUES (17, 'Permission to reset two-factor authentication', 'Reset2FA');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 18)
        INSERT INTO Permissions (Id, Description, Name) VALUES (18, 'Permission to lock a user account', 'LockUser');

        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Id = 19)
        INSERT INTO Permissions (Id, Description, Name) VALUES (19, 'Permission to unlock a user account', 'UnlockUser');
    ");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 19);
        }
    }
}
