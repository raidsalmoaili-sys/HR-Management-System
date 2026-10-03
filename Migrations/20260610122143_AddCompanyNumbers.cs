using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_HR_1.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstablishmentNumber",
                table: "Companies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnifiedNumber",
                table: "Companies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstablishmentNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "UnifiedNumber",
                table: "Companies");
        }
    }
}
