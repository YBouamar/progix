using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Logistique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FabricationLe",
                schema: "projets",
                table: "commandes_usine",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LivreeLe",
                schema: "projets",
                table: "commandes_usine",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecueLe",
                schema: "projets",
                table: "commandes_usine",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuiviPar",
                schema: "projets",
                table: "commandes_usine",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "articles_stock",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StockInitial = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Seuil = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_articles_stock", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mouvements_stock",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Sens = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantite = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Motif = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Par = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mouvements_stock", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_articles_stock_Entite_Code",
                schema: "projets",
                table: "articles_stock",
                columns: new[] { "Entite", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mouvements_stock_Entite_Code",
                schema: "projets",
                table: "mouvements_stock",
                columns: new[] { "Entite", "Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "articles_stock",
                schema: "projets");

            migrationBuilder.DropTable(
                name: "mouvements_stock",
                schema: "projets");

            migrationBuilder.DropColumn(
                name: "FabricationLe",
                schema: "projets",
                table: "commandes_usine");

            migrationBuilder.DropColumn(
                name: "LivreeLe",
                schema: "projets",
                table: "commandes_usine");

            migrationBuilder.DropColumn(
                name: "RecueLe",
                schema: "projets",
                table: "commandes_usine");

            migrationBuilder.DropColumn(
                name: "SuiviPar",
                schema: "projets",
                table: "commandes_usine");
        }
    }
}
