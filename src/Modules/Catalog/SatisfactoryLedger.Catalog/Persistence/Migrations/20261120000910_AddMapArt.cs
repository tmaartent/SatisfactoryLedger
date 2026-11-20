using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SatisfactoryLedger.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMapArt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_art",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    x0 = table.Column<double>(type: "double precision", nullable: false),
                    y0 = table.Column<double>(type: "double precision", nullable: false),
                    width = table.Column<double>(type: "double precision", nullable: false),
                    height = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_art", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "map_art_tiles",
                schema: "catalog",
                columns: table => new
                {
                    column = table.Column<int>(type: "integer", nullable: false),
                    row = table.Column<int>(type: "integer", nullable: false),
                    map_art_id = table.Column<int>(type: "integer", nullable: false),
                    image = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_art_tiles", x => new { x.map_art_id, x.column, x.row });
                    table.ForeignKey(
                        name: "fk_map_art_tiles_map_art_map_art_id",
                        column: x => x.map_art_id,
                        principalSchema: "catalog",
                        principalTable: "map_art",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_art_tiles",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "map_art",
                schema: "catalog");
        }
    }
}
