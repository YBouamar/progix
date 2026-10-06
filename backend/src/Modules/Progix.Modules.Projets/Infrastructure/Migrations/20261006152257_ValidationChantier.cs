using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ValidationChantier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ValidationChantierLe",
                schema: "projets",
                table: "etapes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationChantierPar",
                schema: "projets",
                table: "etapes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Les mises en tension déjà validées ou réalisées gardent leur état : la troisième validation est reprise de la validation administrative.
            migrationBuilder.Sql(@"UPDATE projets.etapes SET ""ValidationChantierLe"" = ""ValidationAdministrativeLe"", ""ValidationChantierPar"" = 'Reprise'
                WHERE ""Numero"" = 9 AND ""ValidationTechniqueLe"" IS NOT NULL AND ""ValidationAdministrativeLe"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidationChantierLe",
                schema: "projets",
                table: "etapes");

            migrationBuilder.DropColumn(
                name: "ValidationChantierPar",
                schema: "projets",
                table: "etapes");
        }
    }
}
