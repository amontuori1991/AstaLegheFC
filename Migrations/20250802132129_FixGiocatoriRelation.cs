using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AstaLegheFC.Migrations
{
    /// <inheritdoc />
    public partial class FixGiocatoriRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Giocatori_Squadre_SquadraId1",
                table: "Giocatori");

            migrationBuilder.DropIndex(
                name: "IX_Giocatori_SquadraId1",
                table: "Giocatori");

            migrationBuilder.DropColumn(
                name: "SquadraId1",
                table: "Giocatori");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SquadraId1",
                table: "Giocatori",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Giocatori_SquadraId1",
                table: "Giocatori",
                column: "SquadraId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Giocatori_Squadre_SquadraId1",
                table: "Giocatori",
                column: "SquadraId1",
                principalTable: "Squadre",
                principalColumn: "Id");
        }
    }
}
