using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "projets");

            migrationBuilder.CreateTable(
                name: "compteurs",
                schema: "projets",
                columns: table => new
                {
                    Cle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Valeur = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compteurs", x => x.Cle);
                });

            migrationBuilder.CreateTable(
                name: "projets",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProprietaireId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProprietaireLibre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TypeConstruction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Statut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Bloque = table.Column<bool>(type: "boolean", nullable: false),
                    BlocageMotif = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BlocagePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BlocageLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Supprime = table.Column<bool>(type: "boolean", nullable: false),
                    SuppressionMotif = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SuppressionPar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SuppressionLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_projets_ClientId",
                schema: "projets",
                table: "projets",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_projets_Code",
                schema: "projets",
                table: "projets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projets_Supprime_Statut",
                schema: "projets",
                table: "projets",
                columns: new[] { "Supprime", "Statut" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compteurs",
                schema: "projets");

            migrationBuilder.DropTable(
                name: "projets",
                schema: "projets");
        }
    }
}
