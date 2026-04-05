using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class FixTranslationColumnsToJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix any empty strings in translation columns to valid JSON.
            // The jsonb conversion is no longer needed — the model uses a
            // string converter, so columns stay as text.
            migrationBuilder.Sql("""
                UPDATE "Products" SET "NameTranslations" = '{}' WHERE "NameTranslations" IS NULL OR "NameTranslations" = '';
                UPDATE "Products" SET "DescriptionTranslations" = '{}' WHERE "DescriptionTranslations" IS NULL OR "DescriptionTranslations" = '';
                UPDATE "BlogPosts" SET "TitleTranslations" = '{}' WHERE "TitleTranslations" IS NULL OR "TitleTranslations" = '';
                UPDATE "BlogPosts" SET "ContentTranslations" = '{}' WHERE "ContentTranslations" IS NULL OR "ContentTranslations" = '';
                UPDATE "BlogPosts" SET "ExcerptTranslations" = '{}' WHERE "ExcerptTranslations" IS NULL OR "ExcerptTranslations" = '';
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to revert — data fix only
        }
    }
}
