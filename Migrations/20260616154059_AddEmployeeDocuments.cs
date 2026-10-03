using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_HR_1.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ContractEndDate",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ContractStartDate",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "HireDate",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "IqamaExpiry",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IqamaNumber",
                table: "Employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "Employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NationalIdExpiry",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalityAr",
                table: "Employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalityCode",
                table: "Employees",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalityEn",
                table: "Employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PassportExpiry",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportNumber",
                table: "Employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractEndDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ContractStartDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "HireDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IqamaExpiry",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IqamaNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "NationalIdExpiry",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "NationalityAr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "NationalityCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "NationalityEn",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PassportExpiry",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PassportNumber",
                table: "Employees");
        }
    }
}
