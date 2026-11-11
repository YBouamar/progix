using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExecutionDalles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArretMotif",
                schema: "projets",
                table: "versions_plans",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DessinateursIds",
                schema: "projets",
                table: "versions_plans",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DessinateursNoms",
                schema: "projets",
                table: "versions_plans",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EnvoyeLe",
                schema: "projets",
                table: "versions_plans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Etat",
                schema: "projets",
                table: "versions_plans",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "en_cours");

            migrationBuilder.AddColumn<decimal>(
                name: "Surface",
                schema: "projets",
                table: "versions_plans",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermineLe",
                schema: "projets",
                table: "versions_plans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminePar",
                schema: "projets",
                table: "versions_plans",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "affectations_execution",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AffectePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Debut = table.Column<DateOnly>(type: "date", nullable: false),
                    Fin = table.Column<DateOnly>(type: "date", nullable: true),
                    RetirePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affectations_execution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affectations_execution_dalles_DalleId",
                        column: x => x.DalleId,
                        principalSchema: "projets",
                        principalTable: "dalles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_versions_plans_TermineLe",
                schema: "projets",
                table: "versions_plans",
                column: "TermineLe");

            migrationBuilder.CreateIndex(
                name: "IX_affectations_execution_DalleId_Fin",
                schema: "projets",
                table: "affectations_execution",
                columns: new[] { "DalleId", "Fin" });

            migrationBuilder.CreateIndex(
                name: "IX_affectations_execution_UserId",
                schema: "projets",
                table: "affectations_execution",
                column: "UserId");
            // Reprise : le dessinateur déjà porté par une dalle devient sa première affectation à l'exécution.
            migrationBuilder.Sql("""
                INSERT INTO projets.affectations_execution ("Id", "DalleId", "UserId", "UserNom", "AffectePar", "Debut", "CreatedAt")
                SELECT gen_random_uuid(), d."Id", d."DessinateurId", COALESCE(d."DessinateurNom", ''), 'Reprise', d."CreatedAt"::date, d."CreatedAt"
                FROM projets.dalles d WHERE d."DessinateurId" IS NOT NULL;
                """);
            // Reprise : une version remplacée par une plus récente, ou dont l'étape « Plans d'exécution » est validée, est un dossier terminé.
            migrationBuilder.Sql("""
                UPDATE projets.versions_plans v SET
                    "Etat" = 'termine',
                    "TermineLe" = COALESCE(
                        (SELECT MIN(w."CreatedAt") FROM projets.versions_plans w WHERE w."DalleId" = v."DalleId" AND w."Lettre" > v."Lettre"),
                        (SELECT e."FaitLe" FROM projets.etapes e WHERE e."DalleId" = v."DalleId" AND e."Numero" = 1)),
                    "TerminePar" = 'Reprise',
                    "Surface" = d."Surface",
                    "DessinateursIds" = d."DessinateurId"::text,
                    "DessinateursNoms" = d."DessinateurNom"
                FROM projets.dalles d
                WHERE d."Id" = v."DalleId" AND (
                    EXISTS (SELECT 1 FROM projets.versions_plans w WHERE w."DalleId" = v."DalleId" AND w."Lettre" > v."Lettre")
                    OR EXISTS (SELECT 1 FROM projets.etapes e WHERE e."DalleId" = v."DalleId" AND e."Numero" = 1 AND e."FaitLe" IS NOT NULL));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "affectations_execution",
                schema: "projets");

            migrationBuilder.DropIndex(
                name: "IX_versions_plans_TermineLe",
                schema: "projets",
                table: "versions_plans");

            migrationBuilder.DropColumn(
                name: "ArretMotif",
                schema: "projets",
                table: "versions_plans");

            migrationBuilder.DropColumn(
                name: "DessinateursIds",
                schema: "projets",
                table: "versions_plans");

            migrationBuilder.DropColumn(
                name: "DessinateursNoms",
                schema: "projets",
                table: "versions_plans");

            migrationBuilder.DropColumn(
                name: "EnvoyeLe",
                schema: "projets",
                table: "versions_plans");

            migrationBuilder.DropColumn(
                name: "Etat",
                schema: "projets",
                table: "versions_plans");

            migrationBuilder.DropColumn(
                name: "Surface",
                schema: "projets",
                table: "versions_plans");

            migrationBuilder.DropColumn(
                name: "TermineLe",
                schema: "projets",
                table: "versions_plans");

            migrationBuilder.DropColumn(
                name: "TerminePar",
                schema: "projets",
                table: "versions_plans");
        }
    }
}
