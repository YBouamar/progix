using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Devis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ParametresDevis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parametres",
                schema: "devis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DureeValidite = table.Column<int>(type: "integer", nullable: false),
                    Modalites = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ModifiePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Echeances = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parametres", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parametres",
                schema: "devis");
        }
    }
}
