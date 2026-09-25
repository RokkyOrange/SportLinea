using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportLinea.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryOfResidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CountryOfResidence",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountryOfResidence",
                table: "AspNetUsers");
        }
    }
}
