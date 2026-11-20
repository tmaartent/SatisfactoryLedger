using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.World.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArrangementBeltFloor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Runs from before the belt floor get its default: 8 foundations of 4 metres.
            migrationBuilder.AddColumn<double>(
                name: "ceiling_metres",
                schema: "world",
                table: "arrangements",
                type: "double precision",
                nullable: false,
                defaultValue: 32.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ceiling_metres",
                schema: "world",
                table: "arrangements");
        }
    }
}
