using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Devis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Entite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devis_Numero_Version",
                schema: "devis",
                table: "devis");

            migrationBuilder.AddColumn<string>(
                name: "Entite",
                schema: "devis",
                table: "devis",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_devis_Entite_Numero_Version",
                schema: "devis",
                table: "devis",
                columns: new[] { "Entite", "Numero", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devis_Entite_Numero_Version",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "Entite",
                schema: "devis",
                table: "devis");

            migrationBuilder.CreateIndex(
                name: "IX_devis_Numero_Version",
                schema: "devis",
                table: "devis",
                columns: new[] { "Numero", "Version" },
                unique: true);
        }
    }
}
