using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_HR_1.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyHeadquarters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeadquartersAr",
                table: "Companies",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeadquartersEn",
                table: "Companies",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeadquartersAr",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "HeadquartersEn",
                table: "Companies");
        }
    }
}
