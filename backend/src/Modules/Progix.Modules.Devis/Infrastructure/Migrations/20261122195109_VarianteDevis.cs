using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Devis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VarianteDevis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Variante",
                schema: "devis",
                table: "devis",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Variante",
                schema: "devis",
                table: "devis");
        }
    }
}
