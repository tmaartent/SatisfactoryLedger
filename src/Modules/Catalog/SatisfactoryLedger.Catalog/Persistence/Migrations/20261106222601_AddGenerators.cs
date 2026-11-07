using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGenerators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "generators_added",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "generators_no_longer_in_game",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "generators_updated",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "generators",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    power_megawatts = table.Column<decimal>(type: "numeric", nullable: false),
                    supplemental_litres_per_megajoule = table.Column<decimal>(type: "numeric", nullable: false),
                    is_in_game = table.Column<bool>(type: "boolean", nullable: false),
                    fuels = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generators", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generators",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "generators_added",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "generators_no_longer_in_game",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "generators_updated",
                schema: "catalog",
                table: "imports");
        }
    }
}
