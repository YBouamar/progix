using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Dalles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dalles",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<char>(type: "character(1)", nullable: false),
                    Bloc = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Immeuble = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Niveau = table.Column<int>(type: "integer", nullable: false),
                    Detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Surface = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dalles_projets_ProjetId",
                        column: x => x.ProjetId,
                        principalSchema: "projets",
                        principalTable: "projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dalles_ProjetId_Numero",
                schema: "projets",
                table: "dalles",
                columns: new[] { "ProjetId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dalles",
                schema: "projets");
        }
    }
}
