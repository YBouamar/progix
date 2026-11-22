using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ForfaitFixeTypeConstruction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ForfaitFixe",
                schema: "projets",
                table: "types_construction",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForfaitFixe",
                schema: "projets",
                table: "types_construction");
        }
    }
}
