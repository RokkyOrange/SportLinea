using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportLinea.Migrations
{
    /// <inheritdoc />
    public partial class AddZeusSpinStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HadesSideWinAmount",
                table: "AccountOperations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZeusSideWinAmount",
                table: "AccountOperations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ZeusSpinKind",
                table: "AccountOperations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HadesSideWinAmount",
                table: "AccountOperations");

            migrationBuilder.DropColumn(
                name: "ZeusSideWinAmount",
                table: "AccountOperations");

            migrationBuilder.DropColumn(
                name: "ZeusSpinKind",
                table: "AccountOperations");
        }
    }
}
