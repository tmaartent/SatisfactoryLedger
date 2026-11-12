using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.World.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropFactoryOutputBelt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "input_items",
                schema: "world",
                table: "factories");

            migrationBuilder.DropColumn(
                name: "output_belt",
                schema: "world",
                table: "factories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "input_items",
                schema: "world",
                table: "factories",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "output_belt",
                schema: "world",
                table: "factories",
                type: "text",
                nullable: true);
        }
    }
}
