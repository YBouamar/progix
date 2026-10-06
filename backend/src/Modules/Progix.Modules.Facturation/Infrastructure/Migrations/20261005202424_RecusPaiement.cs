using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecusPaiement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AncienNumeroRecu",
                schema: "facturation",
                table: "encaissements",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Jeton",
                schema: "facturation",
                table: "encaissements",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroRecu",
                schema: "facturation",
                table: "encaissements",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SigneLe",
                schema: "facturation",
                table: "encaissements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_encaissements_Jeton",
                schema: "facturation",
                table: "encaissements",
                column: "Jeton",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_encaissements_Jeton",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "AncienNumeroRecu",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "Jeton",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "NumeroRecu",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "SigneLe",
                schema: "facturation",
                table: "encaissements");
        }
    }
}
