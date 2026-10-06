using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Devis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "devis");

            migrationBuilder.CreateTable(
                name: "compteurs",
                schema: "devis",
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
                name: "devis",
                schema: "devis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Etat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DateDevis = table.Column<DateOnly>(type: "date", nullable: false),
                    DureeValidite = table.Column<int>(type: "integer", nullable: false),
                    CommercialId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommercialNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RemiseM2 = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RemiseMotif = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ModalitesContrat = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Forfait = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ExonereTva = table.Column<bool>(type: "boolean", nullable: false),
                    SurfaceTotale = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalHT = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RemiseTotale = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    NetHT = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Tva = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CreePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Echeances = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lignes",
                schema: "devis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DevisId = table.Column<Guid>(type: "uuid", nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Emplacement = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Surface = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RatioActif = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    RatioPassif = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    PrixM2 = table.Column<int>(type: "integer", nullable: false),
                    MontantHT = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Systemes = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lignes_devis_DevisId",
                        column: x => x.DevisId,
                        principalSchema: "devis",
                        principalTable: "devis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_devis_Numero_Version",
                schema: "devis",
                table: "devis",
                columns: new[] { "Numero", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devis_ProjetId",
                schema: "devis",
                table: "devis",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_lignes_DevisId",
                schema: "devis",
                table: "lignes",
                column: "DevisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compteurs",
                schema: "devis");

            migrationBuilder.DropTable(
                name: "lignes",
                schema: "devis");

            migrationBuilder.DropTable(
                name: "devis",
                schema: "devis");
        }
    }
}
