using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    public partial class AddConsumerTypeToRateAndConsumer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add AccountType column to Rates (int with default 0)
            migrationBuilder.AddColumn<int>(
                name: "AccountType",
                table: "Rates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Add temporary int column for Consumers.AccountType
            migrationBuilder.AddColumn<int>(
                name: "AccountTypeTemp",
                table: "Consumers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Migrate string values in Consumers.AccountType to int values in AccountTypeTemp
            migrationBuilder.Sql(
                @"
                UPDATE Consumers SET AccountTypeTemp = 
                    CASE AccountType
                        WHEN 'Residential' THEN 0
                        WHEN 'Commercial' THEN 1
                        WHEN 'Institutional' THEN 2
                        ELSE 0
                    END
                ");

            // Drop old string AccountType column
            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Consumers");

            // Rename AccountTypeTemp to AccountType
            migrationBuilder.RenameColumn(
                name: "AccountTypeTemp",
                table: "Consumers",
                newName: "AccountType");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert AccountType in Rates
            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Rates");

            // Add back the string AccountType column in Consumers
            migrationBuilder.AddColumn<string>(
                name: "AccountTypeTemp",
                table: "Consumers",
                type: "nvarchar(max)",
                nullable: true);

            // Convert int values back to strings
            migrationBuilder.Sql(
                @"
                UPDATE Consumers SET AccountTypeTemp = 
                    CASE AccountType
                        WHEN 0 THEN 'Residential'
                        WHEN 1 THEN 'Commercial'
                        WHEN 2 THEN 'Institutional'
                        ELSE 'Residential'
                    END
                ");

            // Drop int AccountType column
            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Consumers");

            // Rename string AccountTypeTemp back to AccountType
            migrationBuilder.RenameColumn(
                name: "AccountTypeTemp",
                table: "Consumers",
                newName: "AccountType");
        }
    }
}
