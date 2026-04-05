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
            // Fix empty strings and invalid JSON in translation columns so they can be cast to jsonb
            migrationBuilder.Sql("""
                UPDATE "Products" SET "NameTranslations" = '{}' WHERE "NameTranslations" IS NULL OR "NameTranslations" = '' OR "NameTranslations" = '""';
                UPDATE "Products" SET "DescriptionTranslations" = '{}' WHERE "DescriptionTranslations" IS NULL OR "DescriptionTranslations" = '' OR "DescriptionTranslations" = '""';
                UPDATE "BlogPosts" SET "TitleTranslations" = '{}' WHERE "TitleTranslations" IS NULL OR "TitleTranslations" = '' OR "TitleTranslations" = '""';
                UPDATE "BlogPosts" SET "ContentTranslations" = '{}' WHERE "ContentTranslations" IS NULL OR "ContentTranslations" = '' OR "ContentTranslations" = '""';
                UPDATE "BlogPosts" SET "ExcerptTranslations" = '{}' WHERE "ExcerptTranslations" IS NULL OR "ExcerptTranslations" = '' OR "ExcerptTranslations" = '""';
            """);

            // Now safely convert to jsonb
            migrationBuilder.Sql("""
                ALTER TABLE "Products" ALTER COLUMN "NameTranslations" SET DEFAULT '{}';
                ALTER TABLE "Products" ALTER COLUMN "NameTranslations" TYPE jsonb USING "NameTranslations"::jsonb;
                ALTER TABLE "Products" ALTER COLUMN "DescriptionTranslations" SET DEFAULT '{}';
                ALTER TABLE "Products" ALTER COLUMN "DescriptionTranslations" TYPE jsonb USING "DescriptionTranslations"::jsonb;
                ALTER TABLE "BlogPosts" ALTER COLUMN "TitleTranslations" SET DEFAULT '{}';
                ALTER TABLE "BlogPosts" ALTER COLUMN "TitleTranslations" TYPE jsonb USING "TitleTranslations"::jsonb;
                ALTER TABLE "BlogPosts" ALTER COLUMN "ContentTranslations" SET DEFAULT '{}';
                ALTER TABLE "BlogPosts" ALTER COLUMN "ContentTranslations" TYPE jsonb USING "ContentTranslations"::jsonb;
                ALTER TABLE "BlogPosts" ALTER COLUMN "ExcerptTranslations" SET DEFAULT '{}';
                ALTER TABLE "BlogPosts" ALTER COLUMN "ExcerptTranslations" TYPE jsonb USING "ExcerptTranslations"::jsonb;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Products" ALTER COLUMN "NameTranslations" TYPE text;
                ALTER TABLE "Products" ALTER COLUMN "DescriptionTranslations" TYPE text;
                ALTER TABLE "BlogPosts" ALTER COLUMN "TitleTranslations" TYPE text;
                ALTER TABLE "BlogPosts" ALTER COLUMN "ContentTranslations" TYPE text;
                ALTER TABLE "BlogPosts" ALTER COLUMN "ExcerptTranslations" TYPE text;
            """);
        }
    }
}
