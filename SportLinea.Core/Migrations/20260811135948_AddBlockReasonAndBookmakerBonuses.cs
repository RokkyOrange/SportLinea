using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportLinea.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockReasonAndBookmakerBonuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwardedByUserId",
                table: "Bonuses",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BookmakerComment",
                table: "Bonuses",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockReason",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedByUserId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bonuses_AwardedByUserId",
                table: "Bonuses",
                column: "AwardedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_BlockedByUserId",
                table: "AspNetUsers",
                column: "BlockedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_BlockedByUserId",
                table: "AspNetUsers",
                column: "BlockedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Bonuses_AspNetUsers_AwardedByUserId",
                table: "Bonuses",
                column: "AwardedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_BlockedByUserId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Bonuses_AspNetUsers_AwardedByUserId",
                table: "Bonuses");

            migrationBuilder.DropIndex(
                name: "IX_Bonuses_AwardedByUserId",
                table: "Bonuses");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_BlockedByUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AwardedByUserId",
                table: "Bonuses");

            migrationBuilder.DropColumn(
                name: "BookmakerComment",
                table: "Bonuses");

            migrationBuilder.DropColumn(
                name: "BlockReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BlockedByUserId",
                table: "AspNetUsers");
        }
    }
}
