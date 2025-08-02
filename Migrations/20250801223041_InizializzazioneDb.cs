using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AstaLegheFC.Migrations
{
    /// <inheritdoc />
    public partial class InizializzazioneDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Leghe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leghe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ListoneCalciatori",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Ruolo = table.Column<string>(type: "text", nullable: false),
                    Squadra = table.Column<string>(type: "text", nullable: false),
                    Quotazione = table.Column<int>(type: "integer", nullable: false),
                    QuotazioneIniziale = table.Column<int>(type: "integer", nullable: false),
                    Codice = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListoneCalciatori", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Squadre",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nickname = table.Column<string>(type: "text", nullable: false),
                    Crediti = table.Column<int>(type: "integer", nullable: false),
                    Portieri = table.Column<int>(type: "integer", nullable: false),
                    Difensori = table.Column<int>(type: "integer", nullable: false),
                    Centrocampisti = table.Column<int>(type: "integer", nullable: false),
                    Attaccanti = table.Column<int>(type: "integer", nullable: false),
                    LegaId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Squadre", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Squadre_Leghe_LegaId",
                        column: x => x.LegaId,
                        principalTable: "Leghe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Giocatori",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    SquadraReale = table.Column<string>(type: "text", nullable: false),
                    Ruolo = table.Column<string>(type: "text", nullable: false),
                    SquadraId = table.Column<int>(type: "integer", nullable: true),
                    IdListone = table.Column<int>(type: "integer", nullable: true),
                    CreditiSpesi = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Giocatori", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Giocatori_Squadre_SquadraId",
                        column: x => x.SquadraId,
                        principalTable: "Squadre",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Giocatori_SquadraId",
                table: "Giocatori",
                column: "SquadraId");

            migrationBuilder.CreateIndex(
                name: "IX_Squadre_LegaId",
                table: "Squadre",
                column: "LegaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Giocatori");

            migrationBuilder.DropTable(
                name: "ListoneCalciatori");

            migrationBuilder.DropTable(
                name: "Squadre");

            migrationBuilder.DropTable(
                name: "Leghe");
        }
    }
}
