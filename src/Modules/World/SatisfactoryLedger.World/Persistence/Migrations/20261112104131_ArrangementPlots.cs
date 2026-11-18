using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.World.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArrangementPlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Runs made on a grid of cells, before plots: their settings mean nothing on plots, and they were made while
            // the arrangement was being built (step 47).
            migrationBuilder.Sql("DELETE FROM world.arrangements;");

            migrationBuilder.RenameColumn(
                name: "gap_y_metres",
                schema: "world",
                table: "arrangements",
                newName: "road_y_metres");

            migrationBuilder.RenameColumn(
                name: "gap_x_metres",
                schema: "world",
                table: "arrangements",
                newName: "road_x_metres");

            migrationBuilder.RenameColumn(
                name: "cell_metres",
                schema: "world",
                table: "arrangements",
                newName: "plot_width_metres");

            migrationBuilder.AddColumn<double>(
                name: "area_x0",
                schema: "world",
                table: "arrangements",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "area_x1",
                schema: "world",
                table: "arrangements",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "area_y0",
                schema: "world",
                table: "arrangements",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "area_y1",
                schema: "world",
                table: "arrangements",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "plot_depth_metres",
                schema: "world",
                table: "arrangements",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "area_x0",
                schema: "world",
                table: "arrangements");

            migrationBuilder.DropColumn(
                name: "area_x1",
                schema: "world",
                table: "arrangements");

            migrationBuilder.DropColumn(
                name: "area_y0",
                schema: "world",
                table: "arrangements");

            migrationBuilder.DropColumn(
                name: "area_y1",
                schema: "world",
                table: "arrangements");

            migrationBuilder.DropColumn(
                name: "plot_depth_metres",
                schema: "world",
                table: "arrangements");

            migrationBuilder.RenameColumn(
                name: "road_y_metres",
                schema: "world",
                table: "arrangements",
                newName: "gap_y_metres");

            migrationBuilder.RenameColumn(
                name: "road_x_metres",
                schema: "world",
                table: "arrangements",
                newName: "gap_x_metres");

            migrationBuilder.RenameColumn(
                name: "plot_width_metres",
                schema: "world",
                table: "arrangements",
                newName: "cell_metres");
        }
    }
}
