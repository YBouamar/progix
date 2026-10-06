using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Equipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DessinateurId",
                schema: "projets",
                table: "dalles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DessinateurNom",
                schema: "projets",
                table: "dalles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "affectations",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fonction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Debut = table.Column<DateOnly>(type: "date", nullable: false),
                    Fin = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affectations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affectations_projets_ProjetId",
                        column: x => x.ProjetId,
                        principalSchema: "projets",
                        principalTable: "projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_affectations_ProjetId_Fonction",
                schema: "projets",
                table: "affectations",
                columns: new[] { "ProjetId", "Fonction" });

            migrationBuilder.CreateIndex(
                name: "IX_affectations_UserId",
                schema: "projets",
                table: "affectations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "affectations",
                schema: "projets");

            migrationBuilder.DropColumn(
                name: "DessinateurId",
                schema: "projets",
                table: "dalles");

            migrationBuilder.DropColumn(
                name: "DessinateurNom",
                schema: "projets",
                table: "dalles");
        }
    }
}
