using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommandesMultiples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_commandes_usine_DalleId",
                schema: "projets",
                table: "commandes_usine");

            migrationBuilder.AddColumn<DateTime>(
                name: "AnnuleeLe",
                schema: "projets",
                table: "commandes_usine",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnuleePar",
                schema: "projets",
                table: "commandes_usine",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Modifications",
                schema: "projets",
                table: "commandes_usine",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotifAnnulation",
                schema: "projets",
                table: "commandes_usine",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Numero",
                schema: "projets",
                table: "commandes_usine",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_commandes_usine_DalleId_Numero",
                schema: "projets",
                table: "commandes_usine",
                columns: new[] { "DalleId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_commandes_usine_DalleId_Numero",
                schema: "projets",
                table: "commandes_usine");

            migrationBuilder.DropColumn(
                name: "AnnuleeLe",
                schema: "projets",
                table: "commandes_usine");

            migrationBuilder.DropColumn(
                name: "AnnuleePar",
                schema: "projets",
                table: "commandes_usine");

            migrationBuilder.DropColumn(
                name: "Modifications",
                schema: "projets",
                table: "commandes_usine");

            migrationBuilder.DropColumn(
                name: "MotifAnnulation",
                schema: "projets",
                table: "commandes_usine");

            migrationBuilder.DropColumn(
                name: "Numero",
                schema: "projets",
                table: "commandes_usine");

            migrationBuilder.CreateIndex(
                name: "IX_commandes_usine_DalleId",
                schema: "projets",
                table: "commandes_usine",
                column: "DalleId",
                unique: true);
        }
    }
}
