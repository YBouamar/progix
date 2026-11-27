using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Devis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RatioPassifParDalle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RatioPassifMoyen",
                schema: "devis",
                table: "lignes",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RatioPassifMoyen",
                schema: "devis",
                table: "lignes");
        }
    }
}
