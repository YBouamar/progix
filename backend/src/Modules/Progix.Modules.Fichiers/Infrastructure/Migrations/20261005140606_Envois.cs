using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Fichiers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Envois : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Complet",
                schema: "fichiers",
                table: "fichiers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EnvoiId",
                schema: "fichiers",
                table: "fichiers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurgeLe",
                schema: "fichiers",
                table: "fichiers",
                type: "timestamp with time zone",
                nullable: true);
        
            // Les fichiers déposés avant l'envoi par morceaux sont complets.
            migrationBuilder.Sql("UPDATE fichiers.fichiers SET \"Complet\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Complet",
                schema: "fichiers",
                table: "fichiers");

            migrationBuilder.DropColumn(
                name: "EnvoiId",
                schema: "fichiers",
                table: "fichiers");

            migrationBuilder.DropColumn(
                name: "PurgeLe",
                schema: "fichiers",
                table: "fichiers");
        }
    }
}
