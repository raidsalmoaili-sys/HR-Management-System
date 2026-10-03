using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_HR_1.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommercialRegistration",
                table: "Companies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommercialRegistration",
                table: "Companies");
        }
    }
}
