using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TensionDeuxCotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "B100",
                schema: "projets",
                table: "mesures_tension",
                type: "numeric(8,1)",
                precision: 8,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "B25",
                schema: "projets",
                table: "mesures_tension",
                type: "numeric(8,1)",
                precision: 8,
                scale: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "B100",
                schema: "projets",
                table: "mesures_tension");

            migrationBuilder.DropColumn(
                name: "B25",
                schema: "projets",
                table: "mesures_tension");
        }
    }
}
