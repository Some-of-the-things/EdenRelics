using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddSeoHealthSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeoHealthSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TakenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalProducts = table.Column<int>(type: "integer", nullable: false),
                    LiveProducts = table.Column<int>(type: "integer", nullable: false),
                    StockProducts = table.Column<int>(type: "integer", nullable: false),
                    SoldProducts = table.Column<int>(type: "integer", nullable: false),
                    ProductsMissingImage = table.Column<int>(type: "integer", nullable: false),
                    ProductsMissingDescription = table.Column<int>(type: "integer", nullable: false),
                    ProductsMissingSlug = table.Column<int>(type: "integer", nullable: false),
                    ProductsMissingSku = table.Column<int>(type: "integer", nullable: false),
                    ProductsWithVideo = table.Column<int>(type: "integer", nullable: false),
                    ProductsWithAdditionalImages = table.Column<int>(type: "integer", nullable: false),
                    AvgProductDescriptionWords = table.Column<int>(type: "integer", nullable: false),
                    TotalBlogPosts = table.Column<int>(type: "integer", nullable: false),
                    PublishedBlogPosts = table.Column<int>(type: "integer", nullable: false),
                    BlogPostsMissingFeaturedImage = table.Column<int>(type: "integer", nullable: false),
                    BlogPostsMissingExcerpt = table.Column<int>(type: "integer", nullable: false),
                    AvgBlogPostWords = table.Column<int>(type: "integer", nullable: false),
                    SitemapUrlCount = table.Column<int>(type: "integer", nullable: false),
                    SitemapImageEntryCount = table.Column<int>(type: "integer", nullable: false),
                    TrackedKeywords = table.Column<int>(type: "integer", nullable: false),
                    TrackedKeywordsWithPosition = table.Column<int>(type: "integer", nullable: false),
                    AvgKeywordPosition = table.Column<double>(type: "double precision", nullable: false),
                    KeywordsInTop10 = table.Column<int>(type: "integer", nullable: false),
                    KeywordsInTop3 = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeoHealthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeoHealthSnapshots_TakenAtUtc",
                table: "SeoHealthSnapshots",
                column: "TakenAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeoHealthSnapshots");
        }
    }
}
