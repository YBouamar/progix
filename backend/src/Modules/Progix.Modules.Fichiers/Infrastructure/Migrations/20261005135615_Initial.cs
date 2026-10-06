using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Fichiers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fichiers");

            migrationBuilder.CreateTable(
                name: "fichiers",
                schema: "fichiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Contexte = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CibleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Taille = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AjouteParId = table.Column<Guid>(type: "uuid", nullable: false),
                    AjoutePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Supprime = table.Column<bool>(type: "boolean", nullable: false),
                    SupprimePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SupprimeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fichiers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fichiers_Contexte_CibleId",
                schema: "fichiers",
                table: "fichiers",
                columns: new[] { "Contexte", "CibleId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fichiers",
                schema: "fichiers");
        }
    }
}
