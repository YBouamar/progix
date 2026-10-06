using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Projets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConformiteDalle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Conforme",
                schema: "projets",
                table: "dalles",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConformiteLe",
                schema: "projets",
                table: "dalles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConformitePar",
                schema: "projets",
                table: "dalles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConformiteRemarque",
                schema: "projets",
                table: "dalles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Conforme",
                schema: "projets",
                table: "dalles");

            migrationBuilder.DropColumn(
                name: "ConformiteLe",
                schema: "projets",
                table: "dalles");

            migrationBuilder.DropColumn(
                name: "ConformitePar",
                schema: "projets",
                table: "dalles");

            migrationBuilder.DropColumn(
                name: "ConformiteRemarque",
                schema: "projets",
                table: "dalles");
        }
    }
}
