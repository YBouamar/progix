using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ParametresCoulis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parametres_coulis",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CimentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CimentKg = table.Column<decimal>(type: "numeric", nullable: true),
                    CimentTemperature = table.Column<decimal>(type: "numeric", nullable: true),
                    Eau = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EauLitres = table.Column<decimal>(type: "numeric", nullable: true),
                    AdjuvantType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AdjuvantGrammes = table.Column<decimal>(type: "numeric", nullable: true),
                    ModifiePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parametres_coulis", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parametres_coulis",
                schema: "projets");
        }
    }
}
