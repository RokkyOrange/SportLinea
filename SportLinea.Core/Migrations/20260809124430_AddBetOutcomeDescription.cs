using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportLinea.Migrations
{
    /// <inheritdoc />
    public partial class AddBetOutcomeDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OutcomeDescription",
                table: "Bets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE b SET b.OutcomeDescription = c.OutcomeDescription " +
                "FROM Bets b INNER JOIN Coefficients c ON b.CoefficientId = c.Id " +
                "WHERE b.OutcomeDescription = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OutcomeDescription",
                table: "Bets");
        }
    }
}
