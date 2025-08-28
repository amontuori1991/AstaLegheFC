using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AstaLegheFC.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxAttaccanti",
                table: "Leghe",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxCentrocampisti",
                table: "Leghe",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxDifensori",
                table: "Leghe",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxPortieri",
                table: "Leghe",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LicenseActivatedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LicenseExpiresAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicensePlan",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxAttaccanti",
                table: "Leghe");

            migrationBuilder.DropColumn(
                name: "MaxCentrocampisti",
                table: "Leghe");

            migrationBuilder.DropColumn(
                name: "MaxDifensori",
                table: "Leghe");

            migrationBuilder.DropColumn(
                name: "MaxPortieri",
                table: "Leghe");

            migrationBuilder.DropColumn(
                name: "LicenseActivatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LicenseExpiresAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LicensePlan",
                table: "AspNetUsers");
        }
    }
}
