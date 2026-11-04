using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.World.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorld : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "world");

            migrationBuilder.CreateTable(
                name: "snapshots",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_name = table.Column<string>(type: "text", nullable: false),
                    game_build = table.Column<int>(type: "integer", nullable: false),
                    saved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    unknown_recipes = table.Column<List<string>>(type: "text[]", nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    counts_connections = table.Column<int>(type: "integer", nullable: false),
                    counts_consumers = table.Column<int>(type: "integer", nullable: false),
                    counts_logistics = table.Column<int>(type: "integer", nullable: false),
                    counts_machines = table.Column<int>(type: "integer", nullable: false),
                    counts_providers = table.Column<int>(type: "integer", nullable: false),
                    unsupported = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_snapshots_imported_at",
                schema: "world",
                table: "snapshots",
                column: "imported_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "snapshots",
                schema: "world");
        }
    }
}
