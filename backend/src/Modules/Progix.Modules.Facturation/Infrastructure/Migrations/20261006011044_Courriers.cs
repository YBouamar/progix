using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Courriers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "courriers",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Entite = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    AncienNumero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    DateCourrier = table.Column<DateOnly>(type: "date", nullable: false),
                    Lieu = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Objet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Entreprise = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Ville = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Bct = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Bet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Modification = table.Column<bool>(type: "boolean", nullable: false),
                    EmisPar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SignePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SigneLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Jeton = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Lignes = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courriers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_courriers_Entite_Numero",
                schema: "facturation",
                table: "courriers",
                columns: new[] { "Entite", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courriers_Jeton",
                schema: "facturation",
                table: "courriers",
                column: "Jeton",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courriers_ProjetId",
                schema: "facturation",
                table: "courriers",
                column: "ProjetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "courriers",
                schema: "facturation");
        }
    }
}
