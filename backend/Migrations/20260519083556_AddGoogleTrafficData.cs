using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleTrafficData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalyticsDailyLandingPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    LandingPage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Sessions = table.Column<int>(type: "integer", nullable: false),
                    EngagedSessions = table.Column<int>(type: "integer", nullable: false),
                    Conversions = table.Column<int>(type: "integer", nullable: false),
                    AverageSessionDuration = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsDailyLandingPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsDailySources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Medium = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sessions = table.Column<int>(type: "integer", nullable: false),
                    Users = table.Column<int>(type: "integer", nullable: false),
                    Conversions = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsDailySources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsDailyTotals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Sessions = table.Column<int>(type: "integer", nullable: false),
                    Users = table.Column<int>(type: "integer", nullable: false),
                    NewUsers = table.Column<int>(type: "integer", nullable: false),
                    EngagedSessions = table.Column<int>(type: "integer", nullable: false),
                    Conversions = table.Column<int>(type: "integer", nullable: false),
                    EngagementRate = table.Column<double>(type: "double precision", nullable: false),
                    AverageSessionDuration = table.Column<double>(type: "double precision", nullable: false),
                    ScreenPageViews = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsDailyTotals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchConsoleDailyPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Page = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Clicks = table.Column<int>(type: "integer", nullable: false),
                    Impressions = table.Column<int>(type: "integer", nullable: false),
                    Ctr = table.Column<double>(type: "double precision", nullable: false),
                    Position = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchConsoleDailyPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchConsoleDailyQueries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Query = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Clicks = table.Column<int>(type: "integer", nullable: false),
                    Impressions = table.Column<int>(type: "integer", nullable: false),
                    Ctr = table.Column<double>(type: "double precision", nullable: false),
                    Position = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchConsoleDailyQueries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchConsoleDailyTotals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Clicks = table.Column<int>(type: "integer", nullable: false),
                    Impressions = table.Column<int>(type: "integer", nullable: false),
                    Ctr = table.Column<double>(type: "double precision", nullable: false),
                    Position = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchConsoleDailyTotals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsDailyLandingPages_Date_LandingPage",
                table: "AnalyticsDailyLandingPages",
                columns: new[] { "Date", "LandingPage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsDailySources_Date_Source_Medium",
                table: "AnalyticsDailySources",
                columns: new[] { "Date", "Source", "Medium" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsDailyTotals_Date",
                table: "AnalyticsDailyTotals",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchConsoleDailyPages_Date_Page",
                table: "SearchConsoleDailyPages",
                columns: new[] { "Date", "Page" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchConsoleDailyQueries_Date_Query",
                table: "SearchConsoleDailyQueries",
                columns: new[] { "Date", "Query" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchConsoleDailyQueries_Query",
                table: "SearchConsoleDailyQueries",
                column: "Query");

            migrationBuilder.CreateIndex(
                name: "IX_SearchConsoleDailyTotals_Date",
                table: "SearchConsoleDailyTotals",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsDailyLandingPages");

            migrationBuilder.DropTable(
                name: "AnalyticsDailySources");

            migrationBuilder.DropTable(
                name: "AnalyticsDailyTotals");

            migrationBuilder.DropTable(
                name: "SearchConsoleDailyPages");

            migrationBuilder.DropTable(
                name: "SearchConsoleDailyQueries");

            migrationBuilder.DropTable(
                name: "SearchConsoleDailyTotals");
        }
    }
}
