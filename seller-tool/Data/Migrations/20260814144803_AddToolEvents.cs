using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdenRelics.SellerTool.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddToolEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToolEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    GarmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    Detail = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToolEvents_GarmentId",
                table: "ToolEvents",
                column: "GarmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolEvents_OccurredAtUtc_Kind",
                table: "ToolEvents",
                columns: new[] { "OccurredAtUtc", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolEvents_OccurredAtUtc_SellerId",
                table: "ToolEvents",
                columns: new[] { "OccurredAtUtc", "SellerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolEvents");
        }
    }
}
