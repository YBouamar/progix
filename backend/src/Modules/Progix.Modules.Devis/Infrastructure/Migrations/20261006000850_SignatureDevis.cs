using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Devis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SignatureDevis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Jeton",
                schema: "devis",
                table: "devis",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SigneLe",
                schema: "devis",
                table: "devis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignePar",
                schema: "devis",
                table: "devis",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_devis_Jeton",
                schema: "devis",
                table: "devis",
                column: "Jeton",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devis_Jeton",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "Jeton",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "SigneLe",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "SignePar",
                schema: "devis",
                table: "devis");
        }
    }
}
