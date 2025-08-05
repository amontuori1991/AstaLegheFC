using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AstaLegheFC.Migrations
{
    /// <inheritdoc />
    public partial class RendiAliasLegaUnicoGlobalmente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === DA COMMENTARE ===
            // perché la colonna "AdminId" su "ListoneCalciatori" esiste già
            // migrationBuilder.AddColumn<string>(
            //     name: "AdminId",
            //     table: "ListoneCalciatori",
            //     type: "text",
            //     nullable: false,
            //     defaultValue: "");

            // === DA COMMENTARE ===
            // perché anche la colonna "AdminId" su "Leghe" esiste già
            // migrationBuilder.AddColumn<string>(
            //     name: "AdminId",
            //     table: "Leghe",
            //     type: "text",
            //     nullable: true);

            // === DA LASCIARE ATTIVO ===
            // Questo è l'unico vero scopo di questa migrazione
            migrationBuilder.CreateIndex(
                name: "IX_Leghe_Alias",
                table: "Leghe",
                column: "Alias",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // === DA LASCIARE ATTIVO ===
            migrationBuilder.DropIndex(
                name: "IX_Leghe_Alias",
                table: "Leghe");

            // === DA COMMENTARE ===
            // migrationBuilder.DropColumn(
            //     name: "AdminId",
            //     table: "ListoneCalciatori");

            // === DA COMMENTARE ===
            // migrationBuilder.DropColumn(
            //     name: "AdminId",
            //     table: "Leghe");
        }
    }
}