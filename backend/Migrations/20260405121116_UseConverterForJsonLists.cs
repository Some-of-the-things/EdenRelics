using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class UseConverterForJsonLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure list columns are text (not jsonb) since the EF converter
            // handles JSON serialization. Idempotent — safe if already text.
            migrationBuilder.Sql("""
                ALTER TABLE "Products" ALTER COLUMN "VideoUrls" TYPE text;
                ALTER TABLE "Products" ALTER COLUMN "AdditionalImageUrls" TYPE text;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No action needed
        }
    }
}
