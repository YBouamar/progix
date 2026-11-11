using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetraitRepartitionDessinateurs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La page « Répartition des dessinateurs » est retirée : ses réglages d'habilitation n'ont plus d'objet.
            migrationBuilder.Sql("""DELETE FROM identity.habilitations WHERE "Module" = 'repartition_dessinateurs';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
