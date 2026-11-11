using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceSeparee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Le module « performance » devient deux modules : un réglage enregistré est repris sur les deux.
            migrationBuilder.Sql("""
                INSERT INTO identity.habilitations ("Id", "RoleId", "Module", "Niveau")
                SELECT gen_random_uuid(), h."RoleId", 'performance_ingenieurs', h."Niveau" FROM identity.habilitations h WHERE h."Module" = 'performance';
                UPDATE identity.habilitations SET "Module" = 'performance_dessinateurs' WHERE "Module" = 'performance';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
