using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddProductWentLiveAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WentLiveAtUtc",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            // Existing listed pieces have no recorded go-live date; their creation date is the
            // best we have, and it keeps the shop in the order it shows today.
            migrationBuilder.Sql(
                "UPDATE \"Products\" SET \"WentLiveAtUtc\" = \"CreatedAtUtc\" WHERE \"Status\" IN (1, 2);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WentLiveAtUtc",
                table: "Products");
        }
    }
}
