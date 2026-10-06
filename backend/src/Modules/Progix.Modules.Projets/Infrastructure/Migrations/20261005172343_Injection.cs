using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Injection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fiches_injection",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AirAmbiant = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Malaxeur = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Pompe = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CimentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CimentKg = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    CimentTemperature = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Eau = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EauLitres = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    AdjuvantType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AdjuvantGrammes = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    ClientNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClientSignature = table.Column<string>(type: "text", nullable: true),
                    SignePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SigneLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Cables = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiches_injection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fiches_injection_dalles_DalleId",
                        column: x => x.DalleId,
                        principalSchema: "projets",
                        principalTable: "dalles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fiches_injection_DalleId",
                schema: "projets",
                table: "fiches_injection",
                column: "DalleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiches_injection",
                schema: "projets");
        }
    }
}
