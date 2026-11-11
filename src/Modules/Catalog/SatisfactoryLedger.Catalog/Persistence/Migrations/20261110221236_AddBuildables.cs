using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "buildables_added",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "buildables_no_longer_in_game",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "buildables_updated",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "buildables",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_in_game = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buildables", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buildables",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "buildables_added",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "buildables_no_longer_in_game",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "buildables_updated",
                schema: "catalog",
                table: "imports");
        }
    }
}
