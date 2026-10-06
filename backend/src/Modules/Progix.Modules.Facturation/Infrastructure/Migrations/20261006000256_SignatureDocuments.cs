using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SignatureDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Jeton",
                schema: "facturation",
                table: "factures",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SigneLe",
                schema: "facturation",
                table: "factures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignePar",
                schema: "facturation",
                table: "factures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Jeton",
                schema: "facturation",
                table: "avoirs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SigneLe",
                schema: "facturation",
                table: "avoirs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignePar",
                schema: "facturation",
                table: "avoirs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_factures_Jeton",
                schema: "facturation",
                table: "factures",
                column: "Jeton",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_avoirs_Jeton",
                schema: "facturation",
                table: "avoirs",
                column: "Jeton",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_factures_Jeton",
                schema: "facturation",
                table: "factures");

            migrationBuilder.DropIndex(
                name: "IX_avoirs_Jeton",
                schema: "facturation",
                table: "avoirs");

            migrationBuilder.DropColumn(
                name: "Jeton",
                schema: "facturation",
                table: "factures");

            migrationBuilder.DropColumn(
                name: "SigneLe",
                schema: "facturation",
                table: "factures");

            migrationBuilder.DropColumn(
                name: "SignePar",
                schema: "facturation",
                table: "factures");

            migrationBuilder.DropColumn(
                name: "Jeton",
                schema: "facturation",
                table: "avoirs");

            migrationBuilder.DropColumn(
                name: "SigneLe",
                schema: "facturation",
                table: "avoirs");

            migrationBuilder.DropColumn(
                name: "SignePar",
                schema: "facturation",
                table: "avoirs");
        }
    }
}
