using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SignatureLivraisons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Jeton",
                schema: "facturation",
                table: "bons_livraison",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SigneLe",
                schema: "facturation",
                table: "bons_livraison",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignePar",
                schema: "facturation",
                table: "bons_livraison",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_bons_livraison_Jeton",
                schema: "facturation",
                table: "bons_livraison",
                column: "Jeton",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bons_livraison_Jeton",
                schema: "facturation",
                table: "bons_livraison");

            migrationBuilder.DropColumn(
                name: "Jeton",
                schema: "facturation",
                table: "bons_livraison");

            migrationBuilder.DropColumn(
                name: "SigneLe",
                schema: "facturation",
                table: "bons_livraison");

            migrationBuilder.DropColumn(
                name: "SignePar",
                schema: "facturation",
                table: "bons_livraison");
        }
    }
}
