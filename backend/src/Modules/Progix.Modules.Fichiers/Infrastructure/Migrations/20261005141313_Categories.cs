using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Fichiers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Categories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Categorie",
                schema: "fichiers",
                table: "fichiers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categorie",
                schema: "fichiers",
                table: "fichiers");
        }
    }
}
