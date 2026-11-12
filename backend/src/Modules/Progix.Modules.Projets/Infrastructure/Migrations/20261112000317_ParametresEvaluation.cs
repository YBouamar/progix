using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ParametresEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parametres_evaluation",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    JoursParMois = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    JoursReference = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    SeuilExcellent = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    SeuilBon = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    SeuilMoyen = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    MoisMinimum = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    PoidsSurface = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    PoidsDossiers = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    ObjectifSurface = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    ObjectifDossiers = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    ModifiePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parametres_evaluation", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parametres_evaluation",
                schema: "projets");
        }
    }
}
