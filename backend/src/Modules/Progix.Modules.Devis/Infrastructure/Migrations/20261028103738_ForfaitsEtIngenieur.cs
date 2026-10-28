using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Devis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ForfaitsEtIngenieur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Forfaits",
                schema: "devis",
                table: "parametres",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IngenieurId",
                schema: "devis",
                table: "devis",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IngenieurNom",
                schema: "devis",
                table: "devis",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Forfaits",
                schema: "devis",
                table: "parametres");

            migrationBuilder.DropColumn(
                name: "IngenieurId",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "IngenieurNom",
                schema: "devis",
                table: "devis");
        }
    }
}
