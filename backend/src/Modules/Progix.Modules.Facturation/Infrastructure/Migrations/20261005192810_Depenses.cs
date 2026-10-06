using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Depenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "depenses",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DateDepense = table.Column<DateOnly>(type: "date", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Montant = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_depenses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_depenses_DateDepense",
                schema: "facturation",
                table: "depenses",
                column: "DateDepense");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "depenses",
                schema: "facturation");
        }
    }
}
