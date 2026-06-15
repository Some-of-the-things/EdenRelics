using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddPageViewDaily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageViewDailies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsBot = table.Column<bool>(type: "boolean", nullable: false),
                    Country = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageViewDailies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageViewDailies_Date",
                table: "PageViewDailies",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_PageViewDailies_Date_Path_IsBot_Country",
                table: "PageViewDailies",
                columns: new[] { "Date", "Path", "IsBot", "Country" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageViewDailies");
        }
    }
}
