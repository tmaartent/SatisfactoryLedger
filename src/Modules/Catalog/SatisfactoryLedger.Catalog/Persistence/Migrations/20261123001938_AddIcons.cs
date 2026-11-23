using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SatisfactoryLedger.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIcons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "icon_set",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_icon_set", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "icons",
                schema: "catalog",
                columns: table => new
                {
                    class_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    icon_set_id = table.Column<int>(type: "integer", nullable: false),
                    image = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_icons", x => new { x.icon_set_id, x.class_name });
                    table.ForeignKey(
                        name: "fk_icons_icon_set_icon_set_id",
                        column: x => x.icon_set_id,
                        principalSchema: "catalog",
                        principalTable: "icon_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "icons",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "icon_set",
                schema: "catalog");
        }
    }
}
