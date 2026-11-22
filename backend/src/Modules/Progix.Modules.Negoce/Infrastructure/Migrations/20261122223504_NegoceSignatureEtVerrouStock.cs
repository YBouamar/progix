using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Negoce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NegoceSignatureEtVerrouStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Jeton",
                schema: "negoce",
                table: "documents_vente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SigneLe",
                schema: "negoce",
                table: "documents_vente",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignePar",
                schema: "negoce",
                table: "documents_vente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionStock",
                schema: "negoce",
                table: "articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Jeton",
                schema: "negoce",
                table: "documents_vente");

            migrationBuilder.DropColumn(
                name: "SigneLe",
                schema: "negoce",
                table: "documents_vente");

            migrationBuilder.DropColumn(
                name: "SignePar",
                schema: "negoce",
                table: "documents_vente");

            migrationBuilder.DropColumn(
                name: "VersionStock",
                schema: "negoce",
                table: "articles");
        }
    }
}
