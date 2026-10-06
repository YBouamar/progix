using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Avoirs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EcheanceEffet",
                schema: "facturation",
                table: "encaissements",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "avoirs",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    AncienNumero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    DateAvoir = table.Column<DateOnly>(type: "date", nullable: false),
                    Motif = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Total = table.Column<bool>(type: "boolean", nullable: false),
                    MontantHT = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    TauxTva = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Tva = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    EmisPar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avoirs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_avoirs_factures_FactureId",
                        column: x => x.FactureId,
                        principalSchema: "facturation",
                        principalTable: "factures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_avoirs_Entite_Numero",
                schema: "facturation",
                table: "avoirs",
                columns: new[] { "Entite", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_avoirs_FactureId",
                schema: "facturation",
                table: "avoirs",
                column: "FactureId");

            migrationBuilder.CreateIndex(
                name: "IX_avoirs_ProjetId",
                schema: "facturation",
                table: "avoirs",
                column: "ProjetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avoirs",
                schema: "facturation");

            migrationBuilder.DropColumn(
                name: "EcheanceEffet",
                schema: "facturation",
                table: "encaissements");
        }
    }
}
