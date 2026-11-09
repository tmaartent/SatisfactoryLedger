using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFootprintVolumes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "volumes",
                schema: "catalog",
                table: "footprints",
                type: "jsonb",
                nullable: false,
                // A list: footprints imported before have no volumes until the Docs file is imported again.
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "volumes",
                schema: "catalog",
                table: "footprints");
        }
    }
}
