using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Contrats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contrats",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    AncienNumero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    DevisId = table.Column<Guid>(type: "uuid", nullable: false),
                    DevisNumero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DateContrat = table.Column<DateOnly>(type: "date", nullable: false),
                    Lieu = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmisPar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SignePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SigneLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Jeton = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Annule = table.Column<bool>(type: "boolean", nullable: false),
                    MotifAnnulation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnnulePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AnnuleLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contrats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contrats_Entite_Numero",
                schema: "facturation",
                table: "contrats",
                columns: new[] { "Entite", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contrats_Jeton",
                schema: "facturation",
                table: "contrats",
                column: "Jeton",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contrats_ProjetId",
                schema: "facturation",
                table: "contrats",
                column: "ProjetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contrats",
                schema: "facturation");
        }
    }
}
