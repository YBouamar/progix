using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Tension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FaitPar",
                schema: "projets",
                table: "etapes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FaitLe",
                schema: "projets",
                table: "etapes",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateTable(
                name: "mesures_tension",
                schema: "projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    Cable = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Theorique = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: false),
                    LectureInitiale = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: true),
                    LectureFinale = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mesures_tension", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mesures_tension_dalles_DalleId",
                        column: x => x.DalleId,
                        principalSchema: "projets",
                        principalTable: "dalles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mesures_tension_DalleId",
                schema: "projets",
                table: "mesures_tension",
                column: "DalleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mesures_tension",
                schema: "projets");

            migrationBuilder.AlterColumn<string>(
                name: "FaitPar",
                schema: "projets",
                table: "etapes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FaitLe",
                schema: "projets",
                table: "etapes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
