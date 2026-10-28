using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaiementsParDalle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_encaissements_factures_FactureId",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.AddColumn<string>(
                name: "Parts",
                schema: "facturation",
                table: "factures",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "FactureId",
                schema: "facturation",
                table: "encaissements",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ClientAdresse",
                schema: "facturation",
                table: "encaissements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientIdentifiants",
                schema: "facturation",
                table: "encaissements",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientNom",
                schema: "facturation",
                table: "encaissements",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Dalles",
                schema: "facturation",
                table: "encaissements",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Entite",
                schema: "facturation",
                table: "encaissements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjetCode",
                schema: "facturation",
                table: "encaissements",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjetId",
                schema: "facturation",
                table: "encaissements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ProjetLibelle",
                schema: "facturation",
                table: "encaissements",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_encaissements_ProjetId",
                schema: "facturation",
                table: "encaissements",
                column: "ProjetId");

            // Lignes existantes : listes vides, remplies au démarrage par la reprise des paiements (Paiements.ReprendreAsync).
            migrationBuilder.Sql("""UPDATE facturation.encaissements SET "Dalles" = '[]' WHERE "Dalles" IS NULL;""");
            migrationBuilder.Sql("""UPDATE facturation.factures SET "Parts" = '[]' WHERE "Parts" IS NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_encaissements_ProjetId",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "Parts",
                schema: "facturation",
                table: "factures");

            migrationBuilder.DropColumn(
                name: "ClientAdresse",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "ClientIdentifiants",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "ClientNom",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "Dalles",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "Entite",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "ProjetCode",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "ProjetId",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.DropColumn(
                name: "ProjetLibelle",
                schema: "facturation",
                table: "encaissements");

            migrationBuilder.AlterColumn<Guid>(
                name: "FactureId",
                schema: "facturation",
                table: "encaissements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_encaissements_factures_FactureId",
                schema: "facturation",
                table: "encaissements",
                column: "FactureId",
                principalSchema: "facturation",
                principalTable: "factures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
