using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NumerosBordereaux : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_courriers_Entite_Numero",
                schema: "facturation",
                table: "courriers");

            migrationBuilder.AddColumn<bool>(
                name: "Annule",
                schema: "facturation",
                table: "courriers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnnuleLe",
                schema: "facturation",
                table: "courriers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnulePar",
                schema: "facturation",
                table: "courriers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DestinataireId",
                schema: "facturation",
                table: "courriers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotifAnnulation",
                schema: "facturation",
                table: "courriers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_courriers_Entite_Type_DestinataireId_Numero",
                schema: "facturation",
                table: "courriers",
                columns: new[] { "Entite", "Type", "DestinataireId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_courriers_Entite_Type_DestinataireId_Numero",
                schema: "facturation",
                table: "courriers");

            migrationBuilder.DropColumn(
                name: "Annule",
                schema: "facturation",
                table: "courriers");

            migrationBuilder.DropColumn(
                name: "AnnuleLe",
                schema: "facturation",
                table: "courriers");

            migrationBuilder.DropColumn(
                name: "AnnulePar",
                schema: "facturation",
                table: "courriers");

            migrationBuilder.DropColumn(
                name: "DestinataireId",
                schema: "facturation",
                table: "courriers");

            migrationBuilder.DropColumn(
                name: "MotifAnnulation",
                schema: "facturation",
                table: "courriers");

            migrationBuilder.CreateIndex(
                name: "IX_courriers_Entite_Numero",
                schema: "facturation",
                table: "courriers",
                columns: new[] { "Entite", "Numero" },
                unique: true);
        }
    }
}
