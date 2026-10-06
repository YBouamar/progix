using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Partenaires.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "partenaires");

            migrationBuilder.CreateTable(
                name: "partenaires",
                schema: "partenaires",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Nature = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RaisonSociale = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Ice = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Rc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IdentifiantFiscal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Patente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Civilite = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Cin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Nationalite = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DateNaissance = table.Column<DateOnly>(type: "date", nullable: true),
                    LieuNaissance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Adresse = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Ville = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Telephone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExonereTva = table.Column<bool>(type: "boolean", nullable: false),
                    Archive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partenaires", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                schema: "partenaires",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartenaireId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Fonction = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Telephone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contacts_partenaires_PartenaireId",
                        column: x => x.PartenaireId,
                        principalSchema: "partenaires",
                        principalTable: "partenaires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_PartenaireId",
                schema: "partenaires",
                table: "contacts",
                column: "PartenaireId");

            migrationBuilder.CreateIndex(
                name: "IX_partenaires_Cin",
                schema: "partenaires",
                table: "partenaires",
                column: "Cin",
                unique: true,
                filter: "\"Cin\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_partenaires_Ice",
                schema: "partenaires",
                table: "partenaires",
                column: "Ice",
                unique: true,
                filter: "\"Ice\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_partenaires_Type",
                schema: "partenaires",
                table: "partenaires",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contacts",
                schema: "partenaires");

            migrationBuilder.DropTable(
                name: "partenaires",
                schema: "partenaires");
        }
    }
}
