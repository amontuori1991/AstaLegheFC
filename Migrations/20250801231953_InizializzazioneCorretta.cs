using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AstaLegheFC.Migrations
{
    /// <inheritdoc />
    public partial class InizializzazioneCorretta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quotazione",
                table: "ListoneCalciatori");

            migrationBuilder.RenameColumn(
                name: "QuotazioneIniziale",
                table: "ListoneCalciatori",
                newName: "IdListone");

            migrationBuilder.RenameColumn(
                name: "Codice",
                table: "ListoneCalciatori",
                newName: "RuoloMantra");

            migrationBuilder.AddColumn<int>(
                name: "Diff",
                table: "ListoneCalciatori",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiffM",
                table: "ListoneCalciatori",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FVM",
                table: "ListoneCalciatori",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FVMM",
                table: "ListoneCalciatori",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QtA",
                table: "ListoneCalciatori",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QtAM",
                table: "ListoneCalciatori",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QtI",
                table: "ListoneCalciatori",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QtIM",
                table: "ListoneCalciatori",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "Leghe",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CreditiIniziali",
                table: "Leghe",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Diff",
                table: "ListoneCalciatori");

            migrationBuilder.DropColumn(
                name: "DiffM",
                table: "ListoneCalciatori");

            migrationBuilder.DropColumn(
                name: "FVM",
                table: "ListoneCalciatori");

            migrationBuilder.DropColumn(
                name: "FVMM",
                table: "ListoneCalciatori");

            migrationBuilder.DropColumn(
                name: "QtA",
                table: "ListoneCalciatori");

            migrationBuilder.DropColumn(
                name: "QtAM",
                table: "ListoneCalciatori");

            migrationBuilder.DropColumn(
                name: "QtI",
                table: "ListoneCalciatori");

            migrationBuilder.DropColumn(
                name: "QtIM",
                table: "ListoneCalciatori");

            migrationBuilder.DropColumn(
                name: "Alias",
                table: "Leghe");

            migrationBuilder.DropColumn(
                name: "CreditiIniziali",
                table: "Leghe");

            migrationBuilder.RenameColumn(
                name: "RuoloMantra",
                table: "ListoneCalciatori",
                newName: "Codice");

            migrationBuilder.RenameColumn(
                name: "IdListone",
                table: "ListoneCalciatori",
                newName: "QuotazioneIniziale");

            migrationBuilder.AddColumn<int>(
                name: "Quotazione",
                table: "ListoneCalciatori",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
