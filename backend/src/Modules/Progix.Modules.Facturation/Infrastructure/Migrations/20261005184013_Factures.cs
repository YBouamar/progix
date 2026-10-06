using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Factures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FactureId",
                schema: "facturation",
                table: "bons_livraison",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "factures",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    AncienNumero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProjetLibelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientIdentifiants = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ClientAdresse = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    DateFacture = table.Column<DateOnly>(type: "date", nullable: false),
                    Echeance = table.Column<DateOnly>(type: "date", nullable: false),
                    Origine = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DevisId = table.Column<Guid>(type: "uuid", nullable: true),
                    DevisNumero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AcomptePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    TotalHT = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    AcompteDeduit = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    NetHT = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    TauxTva = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Tva = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    EmisPar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lignes_facture",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Surface = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    PrixM2 = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    MontantHT = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lignes_facture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lignes_facture_factures_FactureId",
                        column: x => x.FactureId,
                        principalSchema: "facturation",
                        principalTable: "factures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_factures_ClientId",
                schema: "facturation",
                table: "factures",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_factures_Entite_Numero",
                schema: "facturation",
                table: "factures",
                columns: new[] { "Entite", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_factures_ProjetId",
                schema: "facturation",
                table: "factures",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_lignes_facture_FactureId",
                schema: "facturation",
                table: "lignes_facture",
                column: "FactureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lignes_facture",
                schema: "facturation");

            migrationBuilder.DropTable(
                name: "factures",
                schema: "facturation");

            migrationBuilder.DropColumn(
                name: "FactureId",
                schema: "facturation",
                table: "bons_livraison");
        }
    }
}
