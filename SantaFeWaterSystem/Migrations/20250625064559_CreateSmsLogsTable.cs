using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class CreateSmsLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "SmsLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_ConsumerId",
                table: "SmsLogs",
                column: "ConsumerId");

            migrationBuilder.AddForeignKey(
                name: "FK_SmsLogs_Consumers_ConsumerId",
                table: "SmsLogs",
                column: "ConsumerId",
                principalTable: "Consumers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SmsLogs_Consumers_ConsumerId",
                table: "SmsLogs");

            migrationBuilder.DropIndex(
                name: "IX_SmsLogs_ConsumerId",
                table: "SmsLogs");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "SmsLogs");
        }
    }
}
