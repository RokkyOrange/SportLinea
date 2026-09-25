using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportLinea.Migrations
{
    /// <inheritdoc />
    public partial class AddRouletteSpinStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBonusPurchase",
                table: "AccountOperations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RouletteSpinKind",
                table: "AccountOperations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RouletteSpinWon",
                table: "AccountOperations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "DELETE FROM AccountOperations WHERE OperationType IN (4, 5, 6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBonusPurchase",
                table: "AccountOperations");

            migrationBuilder.DropColumn(
                name: "RouletteSpinKind",
                table: "AccountOperations");

            migrationBuilder.DropColumn(
                name: "RouletteSpinWon",
                table: "AccountOperations");
        }
    }
}
