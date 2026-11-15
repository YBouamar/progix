using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ForfaitPropreEtMezzanine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ForfaitPropre",
                schema: "projets",
                table: "projets",
                type: "numeric",
                nullable: true);

            // Niveau « Mezzanine », juste après le rez-de-chaussée, pour les bases déjà initialisées (une base neuve le reçoit au premier démarrage).
            // Ajouté une seule fois : si un niveau de ce nom existe déjà, rien n'est fait ; il reste ensuite réglable dans le Paramétrage.
            migrationBuilder.Sql(@"
DO $$
DECLARE rdc integer;
BEGIN
  IF EXISTS (SELECT 1 FROM projets.niveaux) AND NOT EXISTS (SELECT 1 FROM projets.niveaux WHERE lower(""Libelle"") LIKE 'mezzanine%') THEN
    SELECT ""Ordre"" INTO rdc FROM projets.niveaux WHERE ""Valeur"" = 0;
    IF rdc IS NULL THEN rdc := (SELECT COALESCE(MAX(""Ordre""), -1) FROM projets.niveaux); END IF;
    UPDATE projets.niveaux SET ""Ordre"" = ""Ordre"" + 1 WHERE ""Ordre"" > rdc;
    INSERT INTO projets.niveaux (""Id"", ""Valeur"", ""Libelle"", ""Ordre"", ""Actif"", ""CreatedAt"")
    VALUES (gen_random_uuid(), (SELECT COALESCE(MAX(""Valeur""), 0) + 1 FROM projets.niveaux), 'Mezzanine', rdc + 1, true, now());
  END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForfaitPropre",
                schema: "projets",
                table: "projets");

            // Le niveau « Mezzanine » ajouté par la montée de version reste en base : des dalles ont pu le porter.
        }
    }
}
