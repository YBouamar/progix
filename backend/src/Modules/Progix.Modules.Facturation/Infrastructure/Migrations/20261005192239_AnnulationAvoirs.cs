using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnnulationAvoirs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Remboursement",
                schema: "facturation",
                table: "encaissements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Annule",
                schema: "facturation",
                table: "avoirs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnnuleLe",
                schema: "facturation",
                table: "avoirs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnulePar",
                schema: "facturation",
                table: "avoirs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotifAnnulation",
                schema: "facturation",
                table: "avoirs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Remboursement",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "Annule",
                schema: "facturation",
                table: "avoirs");

            migrationBuilder.DropColumn(
                name: "AnnuleLe",
                schema: "facturation",
                table: "avoirs");

            migrationBuilder.DropColumn(
                name: "AnnulePar",
                schema: "facturation",
                table: "avoirs");

            migrationBuilder.DropColumn(
                name: "MotifAnnulation",
                schema: "facturation",
                table: "avoirs");
        }
    }
}
