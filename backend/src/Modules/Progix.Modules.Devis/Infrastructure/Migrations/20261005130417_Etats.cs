using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Devis.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnnuleLe",
                schema: "devis",
                table: "devis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnuleMotif",
                schema: "devis",
                table: "devis",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EnvoyeLe",
                schema: "devis",
                table: "devis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemplaceLe",
                schema: "devis",
                table: "devis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValideLe",
                schema: "devis",
                table: "devis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidePar",
                schema: "devis",
                table: "devis",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "evenements",
                schema: "devis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DevisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quoi = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Qui = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evenements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evenements_devis_DevisId",
                        column: x => x.DevisId,
                        principalSchema: "devis",
                        principalTable: "devis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evenements_DevisId",
                schema: "devis",
                table: "evenements",
                column: "DevisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evenements",
                schema: "devis");

            migrationBuilder.DropColumn(
                name: "AnnuleLe",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "AnnuleMotif",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "EnvoyeLe",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "RemplaceLe",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "ValideLe",
                schema: "devis",
                table: "devis");

            migrationBuilder.DropColumn(
                name: "ValidePar",
                schema: "devis",
                table: "devis");
        }
    }
}
