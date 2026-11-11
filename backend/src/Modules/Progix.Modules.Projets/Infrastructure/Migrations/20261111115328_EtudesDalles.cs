using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EtudesDalles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EtudeTermineeLe",
                schema: "projets",
                table: "dalles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtudeTermineePar",
                schema: "projets",
                table: "dalles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "affectations_etude",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AffectePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Debut = table.Column<DateOnly>(type: "date", nullable: false),
                    Fin = table.Column<DateOnly>(type: "date", nullable: true),
                    RetirePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affectations_etude", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affectations_etude_dalles_DalleId",
                        column: x => x.DalleId,
                        principalSchema: "projets",
                        principalTable: "dalles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_affectations_etude_DalleId_Fin",
                schema: "projets",
                table: "affectations_etude",
                columns: new[] { "DalleId", "Fin" });

            migrationBuilder.CreateIndex(
                name: "IX_affectations_etude_UserId",
                schema: "projets",
                table: "affectations_etude",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "affectations_etude",
                schema: "projets");

            migrationBuilder.DropColumn(
                name: "EtudeTermineeLe",
                schema: "projets",
                table: "dalles");

            migrationBuilder.DropColumn(
                name: "EtudeTermineePar",
                schema: "projets",
                table: "dalles");
        }
    }
}
