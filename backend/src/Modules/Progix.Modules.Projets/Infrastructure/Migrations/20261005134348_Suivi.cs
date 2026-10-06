using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Suivi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EtapesFaites",
                schema: "projets",
                table: "dalles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "etape_traces",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Motif = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Par = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_etape_traces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_etape_traces_dalles_DalleId",
                        column: x => x.DalleId,
                        principalSchema: "projets",
                        principalTable: "dalles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "etapes",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    FaitPar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FaitLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Remarque = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ValidationTechniquePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValidationTechniqueLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidationAdministrativePar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValidationAdministrativeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_etapes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_etapes_dalles_DalleId",
                        column: x => x.DalleId,
                        principalSchema: "projets",
                        principalTable: "dalles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_etape_traces_DalleId",
                schema: "projets",
                table: "etape_traces",
                column: "DalleId");

            migrationBuilder.CreateIndex(
                name: "IX_etapes_DalleId_Numero",
                schema: "projets",
                table: "etapes",
                columns: new[] { "DalleId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "etape_traces",
                schema: "projets");

            migrationBuilder.DropTable(
                name: "etapes",
                schema: "projets");

            migrationBuilder.DropColumn(
                name: "EtapesFaites",
                schema: "projets",
                table: "dalles");
        }
    }
}
