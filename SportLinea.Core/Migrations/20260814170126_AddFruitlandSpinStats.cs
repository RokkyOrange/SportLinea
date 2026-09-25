using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportLinea.Migrations
{
    /// <inheritdoc />
    public partial class AddFruitlandSpinStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FruitlandGlobalMultiplier",
                table: "AccountOperations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FruitlandSpinKind",
                table: "AccountOperations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FruitlandGlobalMultiplier",
                table: "AccountOperations");

            migrationBuilder.DropColumn(
                name: "FruitlandSpinKind",
                table: "AccountOperations");
        }
    }
}
