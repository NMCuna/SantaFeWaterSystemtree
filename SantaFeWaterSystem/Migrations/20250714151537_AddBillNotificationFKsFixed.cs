using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddBillNotificationFKsFixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BillNotifications_ConsumerId",
                table: "BillNotifications",
                column: "ConsumerId");

            migrationBuilder.AddForeignKey(
                name: "FK_BillNotifications_Consumers_ConsumerId",
                table: "BillNotifications",
                column: "ConsumerId",
                principalTable: "Consumers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillNotifications_Consumers_ConsumerId",
                table: "BillNotifications");

            migrationBuilder.DropIndex(
                name: "IX_BillNotifications_ConsumerId",
                table: "BillNotifications");
        }
    }
}
