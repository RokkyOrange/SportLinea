using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportLinea.Migrations
{
    /// <inheritdoc />
    public partial class AddCamelotSpinStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CamelotCollectorNominal",
                table: "AccountOperations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CamelotSpinKind",
                table: "AccountOperations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CamelotCollectorNominal",
                table: "AccountOperations");

            migrationBuilder.DropColumn(
                name: "CamelotSpinKind",
                table: "AccountOperations");
        }
    }
}
