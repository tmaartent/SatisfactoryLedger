using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDronePorts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "drone_ports_added",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "drone_ports_no_longer_in_game",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "drone_ports_updated",
                schema: "catalog",
                table: "imports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "drone_ports",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    trip_energy_megajoules = table.Column<decimal>(type: "numeric", nullable: false),
                    trip_energy_per_metre_megajoules = table.Column<decimal>(type: "numeric", nullable: false),
                    is_in_game = table.Column<bool>(type: "boolean", nullable: false),
                    fuels = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drone_ports", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drone_ports",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "drone_ports_added",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "drone_ports_no_longer_in_game",
                schema: "catalog",
                table: "imports");

            migrationBuilder.DropColumn(
                name: "drone_ports_updated",
                schema: "catalog",
                table: "imports");
        }
    }
}
