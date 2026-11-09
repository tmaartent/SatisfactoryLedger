using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.World.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreFactoryMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "output_belt",
                schema: "world",
                table: "factories",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<List<string>>(
                name: "input_items",
                schema: "world",
                table: "factories",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(List<string>),
                oldType: "text[]");

            migrationBuilder.AddColumn<string>(
                name: "machines",
                schema: "world",
                table: "factories",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "machines",
                schema: "world",
                table: "factories");

            migrationBuilder.AlterColumn<string>(
                name: "output_belt",
                schema: "world",
                table: "factories",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<List<string>>(
                name: "input_items",
                schema: "world",
                table: "factories",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);
        }
    }
}
