using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_HR_1.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBranchFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressAr",
                table: "Branches");

            migrationBuilder.RenameColumn(
                name: "ManagerAr",
                table: "Branches",
                newName: "StreetName");

            migrationBuilder.RenameColumn(
                name: "CityEn",
                table: "Branches",
                newName: "DistrictName");

            migrationBuilder.RenameColumn(
                name: "CityAr",
                table: "Branches",
                newName: "City");

            migrationBuilder.AlterColumn<string>(
                name: "NameEn",
                table: "Branches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "BuildingNumber",
                table: "Branches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LicenseEndDate",
                table: "Branches",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                table: "Branches",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LicenseStartDate",
                table: "Branches",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NatAddressRegDate",
                table: "Branches",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NatAddressShort",
                table: "Branches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Branches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryNumber",
                table: "Branches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "VatEndDate",
                table: "Branches",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatNumber",
                table: "Branches",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "VatStartDate",
                table: "Branches",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildingNumber",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "LicenseEndDate",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "LicenseStartDate",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "NatAddressRegDate",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "NatAddressShort",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "SecondaryNumber",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "VatEndDate",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "VatNumber",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "VatStartDate",
                table: "Branches");

            migrationBuilder.RenameColumn(
                name: "StreetName",
                table: "Branches",
                newName: "ManagerAr");

            migrationBuilder.RenameColumn(
                name: "DistrictName",
                table: "Branches",
                newName: "CityEn");

            migrationBuilder.RenameColumn(
                name: "City",
                table: "Branches",
                newName: "CityAr");

            migrationBuilder.AlterColumn<string>(
                name: "NameEn",
                table: "Branches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressAr",
                table: "Branches",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
