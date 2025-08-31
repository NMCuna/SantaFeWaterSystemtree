using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateConsumerSplitNameAndAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "Consumers",
                newName: "LastName");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "Consumers",
                newName: "HomeAddress");

            migrationBuilder.RenameColumn(
                name: "AccountNumber",
                table: "Consumers",
                newName: "MiddleName");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Consumers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MeterAddress",
                table: "Consumers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Consumers");

            migrationBuilder.DropColumn(
                name: "MeterAddress",
                table: "Consumers");

            migrationBuilder.RenameColumn(
                name: "MiddleName",
                table: "Consumers",
                newName: "AccountNumber");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "Consumers",
                newName: "FullName");

            migrationBuilder.RenameColumn(
                name: "HomeAddress",
                table: "Consumers",
                newName: "Address");
        }
    }
}
