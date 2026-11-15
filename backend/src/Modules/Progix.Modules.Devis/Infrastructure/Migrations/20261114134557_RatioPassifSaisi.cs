using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Devis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RatioPassifSaisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RatioPassifSaisi",
                schema: "devis",
                table: "devis",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RatioPassifSaisi",
                schema: "devis",
                table: "devis");
        }
    }
}
