using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Encaissements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "encaissements",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatePaiement = table.Column<DateOnly>(type: "date", nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Montant = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Banque = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SaisiPar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Annule = table.Column<bool>(type: "boolean", nullable: false),
                    MotifAnnulation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnnulePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AnnuleLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encaissements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_encaissements_factures_FactureId",
                        column: x => x.FactureId,
                        principalSchema: "facturation",
                        principalTable: "factures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_encaissements_ClientId_DatePaiement",
                schema: "facturation",
                table: "encaissements",
                columns: new[] { "ClientId", "DatePaiement" });

            migrationBuilder.CreateIndex(
                name: "IX_encaissements_FactureId",
                schema: "facturation",
                table: "encaissements",
                column: "FactureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "encaissements",
                schema: "facturation");
        }
    }
}
