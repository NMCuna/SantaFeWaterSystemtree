using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexesAndSeedPrivacyPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "PrivacyPolicies",
                columns: new[] { "Id", "Content", "CreatedAt", "Title", "Version" },
                values: new object[] { 1, "This is the default privacy policy.", new DateTime(2025, 8, 13, 17, 16, 6, 363, DateTimeKind.Utc).AddTicks(4039), "Default Privacy Policy", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PrivacyPolicies",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
