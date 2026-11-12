using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progix.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PresenceUtilisateurs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DateArrivee",
                schema: "identity",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateDepart",
                schema: "identity",
                table: "users",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateArrivee",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DateDepart",
                schema: "identity",
                table: "users");
        }
    }
}
