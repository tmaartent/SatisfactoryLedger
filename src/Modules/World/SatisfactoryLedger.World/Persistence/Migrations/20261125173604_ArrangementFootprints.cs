using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.World.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArrangementFootprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "footprints",
                schema: "world",
                table: "arrangements",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "footprints",
                schema: "world",
                table: "arrangements");
        }
    }
}
