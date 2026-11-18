using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatisfactoryLedger.World.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArrangements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arrangements",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world = table.Column<string>(type: "text", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cell_metres = table.Column<double>(type: "double precision", nullable: false),
                    gap_x_metres = table.Column<double>(type: "double precision", nullable: false),
                    gap_y_metres = table.Column<double>(type: "double precision", nullable: false),
                    seconds = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    grid_origin_x = table.Column<double>(type: "double precision", nullable: true),
                    grid_origin_y = table.Column<double>(type: "double precision", nullable: true),
                    problem = table.Column<string>(type: "jsonb", nullable: true),
                    proposal = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arrangements", x => x.id);
                    table.ForeignKey(
                        name: "fk_arrangements_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalSchema: "world",
                        principalTable: "snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_arrangements_snapshot_id",
                schema: "world",
                table: "arrangements",
                column: "snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_arrangements_world",
                schema: "world",
                table: "arrangements",
                column: "world");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arrangements",
                schema: "world");
        }
    }
}
