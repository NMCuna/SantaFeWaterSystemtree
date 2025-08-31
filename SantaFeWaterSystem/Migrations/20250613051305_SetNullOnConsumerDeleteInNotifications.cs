using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class SetNullOnConsumerDeleteInNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Consumers_ConsumerId",
                table: "Notifications");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Consumers_ConsumerId",
                table: "Notifications",
                column: "ConsumerId",
                principalTable: "Consumers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Consumers_ConsumerId",
                table: "Notifications");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Consumers_ConsumerId",
                table: "Notifications",
                column: "ConsumerId",
                principalTable: "Consumers",
                principalColumn: "Id");
        }
    }
}
