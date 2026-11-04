using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "buildings",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    somersloop_slots = table.Column<int>(type: "integer", nullable: false),
                    is_in_game = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buildings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "imports",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_version = table.Column<string>(type: "text", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    no_longer_in_game = table.Column<string[]>(type: "text[]", nullable: false),
                    buildings_added = table.Column<int>(type: "integer", nullable: false),
                    buildings_no_longer_in_game = table.Column<int>(type: "integer", nullable: false),
                    buildings_updated = table.Column<int>(type: "integer", nullable: false),
                    items_added = table.Column<int>(type: "integer", nullable: false),
                    items_no_longer_in_game = table.Column<int>(type: "integer", nullable: false),
                    items_updated = table.Column<int>(type: "integer", nullable: false),
                    recipes_added = table.Column<int>(type: "integer", nullable: false),
                    recipes_no_longer_in_game = table.Column<int>(type: "integer", nullable: false),
                    recipes_updated = table.Column<int>(type: "integer", nullable: false),
                    transport_tiers_added = table.Column<int>(type: "integer", nullable: false),
                    transport_tiers_no_longer_in_game = table.Column<int>(type: "integer", nullable: false),
                    transport_tiers_updated = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "items",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    item_form = table.Column<string>(type: "text", nullable: false),
                    is_raw_resource = table.Column<bool>(type: "boolean", nullable: false),
                    is_in_game = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recipes",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    building_id = table.Column<string>(type: "text", nullable: false),
                    cycle_seconds = table.Column<decimal>(type: "numeric", nullable: false),
                    is_alternate = table.Column<bool>(type: "boolean", nullable: false),
                    is_in_game = table.Column<bool>(type: "boolean", nullable: false),
                    inputs = table.Column<string>(type: "jsonb", nullable: false),
                    outputs = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transport_tiers",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    form = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    is_in_game = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_tiers", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buildings",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "imports",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "items",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "recipes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "transport_tiers",
                schema: "catalog");
        }
    }
}
