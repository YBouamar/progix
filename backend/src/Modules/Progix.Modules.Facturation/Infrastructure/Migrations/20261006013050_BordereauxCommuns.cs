using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BordereauxCommuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_courriers_Entite_Type_DestinataireId_Numero",
                schema: "facturation",
                table: "courriers");

            migrationBuilder.CreateIndex(
                name: "IX_courriers_Type_DestinataireId_Numero",
                schema: "facturation",
                table: "courriers",
                columns: new[] { "Type", "DestinataireId", "Numero" },
                unique: true,
                filter: "\"Annule\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_courriers_Type_DestinataireId_Numero",
                schema: "facturation",
                table: "courriers");

            migrationBuilder.CreateIndex(
                name: "IX_courriers_Entite_Type_DestinataireId_Numero",
                schema: "facturation",
                table: "courriers",
                columns: new[] { "Entite", "Type", "DestinataireId", "Numero" },
                unique: true);
        }
    }
}
