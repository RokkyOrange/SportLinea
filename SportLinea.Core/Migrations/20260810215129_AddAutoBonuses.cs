using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportLinea.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoBonuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Bonuses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UsesRemaining",
                table: "Bonuses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UsesTotal",
                table: "Bonuses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BonusId",
                table: "Bets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BetBonusTier",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RouletteBonusTier",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFreeBonus",
                table: "AccountOperations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Bets_BonusId",
                table: "Bets",
                column: "BonusId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bets_Bonuses_BonusId",
                table: "Bets",
                column: "BonusId",
                principalTable: "Bonuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bets_Bonuses_BonusId",
                table: "Bets");

            migrationBuilder.DropIndex(
                name: "IX_Bets_BonusId",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Bonuses");

            migrationBuilder.DropColumn(
                name: "UsesRemaining",
                table: "Bonuses");

            migrationBuilder.DropColumn(
                name: "UsesTotal",
                table: "Bonuses");

            migrationBuilder.DropColumn(
                name: "BonusId",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "BetBonusTier",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RouletteBonusTier",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsFreeBonus",
                table: "AccountOperations");
        }
    }
}
