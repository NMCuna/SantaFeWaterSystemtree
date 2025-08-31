using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SantaFeWaterSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDisconnectionModelAndConsumerStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDisconnected",
                table: "Disconnections");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Disconnections");

            migrationBuilder.DropColumn(
                name: "ReconnectionDate",
                table: "Disconnections");

            migrationBuilder.RenameColumn(
                name: "DisconnectionDate",
                table: "Disconnections",
                newName: "DatePerformed");

            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "Disconnections",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PerformedBy",
                table: "Disconnections",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Disconnections",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDisconnected",
                table: "Consumers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Action",
                table: "Disconnections");

            migrationBuilder.DropColumn(
                name: "PerformedBy",
                table: "Disconnections");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Disconnections");

            migrationBuilder.DropColumn(
                name: "IsDisconnected",
                table: "Consumers");

            migrationBuilder.RenameColumn(
                name: "DatePerformed",
                table: "Disconnections",
                newName: "DisconnectionDate");

            migrationBuilder.AddColumn<bool>(
                name: "IsDisconnected",
                table: "Disconnections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Disconnections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReconnectionDate",
                table: "Disconnections",
                type: "datetime2",
                nullable: true);
        }
    }
}
