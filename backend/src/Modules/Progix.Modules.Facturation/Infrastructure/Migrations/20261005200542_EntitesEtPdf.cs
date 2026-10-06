using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Facturation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntitesEtPdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModeReglement",
                schema: "facturation",
                table: "factures",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "entites",
                schema: "facturation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    RaisonSociale = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FormeJuridique = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Capital = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: true),
                    Siege = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Ville = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Telephone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdentifiantFiscal = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Rc = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TribunalRc = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Patente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Cnss = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Banque = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Rib = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    LogoType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiePar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entites_Code",
                schema: "facturation",
                table: "entites",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entites",
                schema: "facturation");

            migrationBuilder.DropColumn(
                name: "ModeReglement",
                schema: "facturation",
                table: "factures");
        }
    }
}
