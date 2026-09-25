using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportLinea.Migrations
{
    /// <inheritdoc />
    public partial class AddPlinkoSpinStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlinkoBountyMultiplier",
                table: "AccountOperations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlinkoSpinKind",
                table: "AccountOperations",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "DELETE FROM AccountOperations WHERE OperationType IN (7, 8)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlinkoBountyMultiplier",
                table: "AccountOperations");

            migrationBuilder.DropColumn(
                name: "PlinkoSpinKind",
                table: "AccountOperations");
        }
    }
}
