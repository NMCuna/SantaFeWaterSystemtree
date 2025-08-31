using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class LinkBillingToDisconnectionFixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingId",
                table: "Disconnections",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disconnections_BillingId",
                table: "Disconnections",
                column: "BillingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Disconnections_Billings_BillingId",
                table: "Disconnections",
                column: "BillingId",
                principalTable: "Billings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Disconnections_Billings_BillingId",
                table: "Disconnections");

            migrationBuilder.DropIndex(
                name: "IX_Disconnections_BillingId",
                table: "Disconnections");

            migrationBuilder.DropColumn(
                name: "BillingId",
                table: "Disconnections");
        }
    }
}
