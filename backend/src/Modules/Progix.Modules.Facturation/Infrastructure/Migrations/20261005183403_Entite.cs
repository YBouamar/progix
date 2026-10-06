using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Entite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bons_livraison_Numero",
                schema: "facturation",
                table: "bons_livraison");

            migrationBuilder.AddColumn<string>(
                name: "AncienNumero",
                schema: "facturation",
                table: "bons_livraison",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Entite",
                schema: "facturation",
                table: "bons_livraison",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_bons_livraison_Entite_Numero",
                schema: "facturation",
                table: "bons_livraison",
                columns: new[] { "Entite", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bons_livraison_Entite_Numero",
                schema: "facturation",
                table: "bons_livraison");

            migrationBuilder.DropColumn(
                name: "AncienNumero",
                schema: "facturation",
                table: "bons_livraison");

            migrationBuilder.DropColumn(
                name: "Entite",
                schema: "facturation",
                table: "bons_livraison");

            migrationBuilder.CreateIndex(
                name: "IX_bons_livraison_Numero",
                schema: "facturation",
                table: "bons_livraison",
                column: "Numero",
                unique: true);
        }
    }
}
