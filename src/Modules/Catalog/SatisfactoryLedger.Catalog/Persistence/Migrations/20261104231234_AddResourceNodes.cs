using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "resource_nodes_added",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "resource_nodes_no_longer_in_game",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "resource_nodes_updated",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "catalog",
                table: "imports",
                type: "text",
                nullable: false,
                // Edited by hand: every import before this migration read the Docs file.
                defaultValue: "DocsFile");

            migrationBuilder.CreateTable(
                name: "resource_nodes",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    resource = table.Column<string>(type: "text", nullable: false),
                    purity = table.Column<string>(type: "text", nullable: false),
                    is_in_game = table.Column<bool>(type: "boolean", nullable: false),
                    position_x = table.Column<double>(type: "double precision", nullable: false),
                    position_y = table.Column<double>(type: "double precision", nullable: false),
                    position_z = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_nodes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resource_nodes_resource",
                schema: "catalog",
                table: "resource_nodes",
                column: "resource");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_nodes",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "resource_nodes_added",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "resource_nodes_no_longer_in_game",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "resource_nodes_updated",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "catalog",
                table: "imports");
        }
    }
}
