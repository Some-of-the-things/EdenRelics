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
            // Fix empty strings and invalid JSON so they can be cast to jsonb.
            // On fresh databases the defaults are already '{}', so these UPDATEs are harmless.
            migrationBuilder.Sql("""
                UPDATE "Products" SET "NameTranslations" = '{}' WHERE "NameTranslations" IS NULL OR "NameTranslations" = '';
                UPDATE "Products" SET "DescriptionTranslations" = '{}' WHERE "DescriptionTranslations" IS NULL OR "DescriptionTranslations" = '';
                UPDATE "BlogPosts" SET "TitleTranslations" = '{}' WHERE "TitleTranslations" IS NULL OR "TitleTranslations" = '';
                UPDATE "BlogPosts" SET "ContentTranslations" = '{}' WHERE "ContentTranslations" IS NULL OR "ContentTranslations" = '';
                UPDATE "BlogPosts" SET "ExcerptTranslations" = '{}' WHERE "ExcerptTranslations" IS NULL OR "ExcerptTranslations" = '';
            """);

            // Convert text columns to jsonb (safe now that all values are valid JSON)
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
