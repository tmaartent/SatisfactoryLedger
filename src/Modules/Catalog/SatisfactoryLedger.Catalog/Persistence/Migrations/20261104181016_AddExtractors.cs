using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "extractors_added",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "extractors_no_longer_in_game",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "extractors_updated",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "extractors",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    form = table.Column<string>(type: "text", nullable: false),
                    amount_per_cycle = table.Column<decimal>(type: "numeric", nullable: false),
                    cycle_seconds = table.Column<decimal>(type: "numeric", nullable: false),
                    is_in_game = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_extractors", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "extractors",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "extractors_added",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "extractors_no_longer_in_game",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "extractors_updated",
                schema: "catalog",
                table: "imports");
        }
    }
}
