using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartenairesEtRemarques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projet_partenaires",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PartenaireId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projet_partenaires", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projet_partenaires_projets_ProjetId",
                        column: x => x.ProjetId,
                        principalSchema: "projets",
                        principalTable: "projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "remarques",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuteurId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuteurNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AuteurRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Texte = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ModifiePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Supprime = table.Column<bool>(type: "boolean", nullable: false),
                    SupprimePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SupprimeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_remarques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_remarques_projets_ProjetId",
                        column: x => x.ProjetId,
                        principalSchema: "projets",
                        principalTable: "projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "remarque_traces",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RemarqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AncienTexte = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Par = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_remarque_traces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_remarque_traces_remarques_RemarqueId",
                        column: x => x.RemarqueId,
                        principalSchema: "projets",
                        principalTable: "remarques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_projet_partenaires_ProjetId_Role",
                schema: "projets",
                table: "projet_partenaires",
                columns: new[] { "ProjetId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_remarque_traces_RemarqueId",
                schema: "projets",
                table: "remarque_traces",
                column: "RemarqueId");

            migrationBuilder.CreateIndex(
                name: "IX_remarques_ProjetId",
                schema: "projets",
                table: "remarques",
                column: "ProjetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "projet_partenaires",
                schema: "projets");

            migrationBuilder.DropTable(
                name: "remarque_traces",
                schema: "projets");

            migrationBuilder.DropTable(
                name: "remarques",
                schema: "projets");
        }
    }
}
