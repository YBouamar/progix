using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SuiviContrat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EnvoyeLe",
                schema: "facturation",
                table: "contrats",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvoyePar",
                schema: "facturation",
                table: "contrats",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Statut",
                schema: "facturation",
                table: "contrats",
                type: "text",
                nullable: false,
                defaultValue: "en_attente");

            migrationBuilder.AddColumn<DateTime>(
                name: "ValideClientLe",
                schema: "facturation",
                table: "contrats",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValideClientPar",
                schema: "facturation",
                table: "contrats",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnvoyeLe",
                schema: "facturation",
                table: "contrats");

            migrationBuilder.DropColumn(
                name: "EnvoyePar",
                schema: "facturation",
                table: "contrats");

            migrationBuilder.DropColumn(
                name: "Statut",
                schema: "facturation",
                table: "contrats");

            migrationBuilder.DropColumn(
                name: "ValideClientLe",
                schema: "facturation",
                table: "contrats");

            migrationBuilder.DropColumn(
                name: "ValideClientPar",
                schema: "facturation",
                table: "contrats");
        }
    }
}
