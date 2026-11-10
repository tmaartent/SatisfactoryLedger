using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoragesAndStackSizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "stack_size",
                schema: "catalog",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "storages_added",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "storages_no_longer_in_game",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "storages_updated",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "storages",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slots = table.Column<int>(type: "integer", nullable: false),
                    fluid_cubic_metres = table.Column<decimal>(type: "numeric", nullable: false),
                    is_in_game = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_storages", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storages",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "stack_size",
                schema: "catalog",
                table: "items");

            migrationBuilder.DropColumn(
                name: "storages_added",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "storages_no_longer_in_game",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "storages_updated",
                schema: "catalog",
                table: "imports");
        }
    }
}
