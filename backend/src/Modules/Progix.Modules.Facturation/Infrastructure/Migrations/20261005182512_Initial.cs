using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "facturation");

            migrationBuilder.CreateTable(
                name: "bons_livraison",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    DateLivraison = table.Column<DateOnly>(type: "date", nullable: false),
                    Surface = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EmisPar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bons_livraison", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "compteurs",
                schema: "facturation",
                columns: table => new
                {
                    Cle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Valeur = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compteurs", x => x.Cle);
                });

            migrationBuilder.CreateTable(
                name: "lignes_livraison",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BonLivraisonId = table.Column<Guid>(type: "uuid", nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Emplacement = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Surface = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lignes_livraison", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lignes_livraison_bons_livraison_BonLivraisonId",
                        column: x => x.BonLivraisonId,
                        principalSchema: "facturation",
                        principalTable: "bons_livraison",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bons_livraison_Numero",
                schema: "facturation",
                table: "bons_livraison",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bons_livraison_ProjetId",
                schema: "facturation",
                table: "bons_livraison",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_lignes_livraison_BonLivraisonId",
                schema: "facturation",
                table: "lignes_livraison",
                column: "BonLivraisonId");

            migrationBuilder.CreateIndex(
                name: "IX_lignes_livraison_DalleId",
                schema: "facturation",
                table: "lignes_livraison",
                column: "DalleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compteurs",
                schema: "facturation");

            migrationBuilder.DropTable(
                name: "lignes_livraison",
                schema: "facturation");

            migrationBuilder.DropTable(
                name: "bons_livraison",
                schema: "facturation");
        }
    }
}
