using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <summary>
    /// L'injection exige désormais aussi la validation du responsable des chantiers (règle du 2026-10-23).
    /// Migration de données seule, sans changement de modèle : les injections déjà validées gardent leur état,
    /// la troisième validation est reprise de la validation administrative.
    /// </summary>
    [DbContext(typeof(ProjetsDbContext))]
    [Migration("20261023090000_ValidationChantierInjection")]
    public partial class ValidationChantierInjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE projets.etapes SET ""ValidationChantierLe"" = ""ValidationAdministrativeLe"", ""ValidationChantierPar"" = 'Reprise'
                WHERE ""Numero"" = 10 AND ""ValidationChantierLe"" IS NULL AND ""ValidationTechniqueLe"" IS NOT NULL AND ""ValidationAdministrativeLe"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE projets.etapes SET ""ValidationChantierLe"" = NULL, ""ValidationChantierPar"" = NULL
                WHERE ""Numero"" = 10 AND ""ValidationChantierPar"" = 'Reprise';");
        }
    }
}
