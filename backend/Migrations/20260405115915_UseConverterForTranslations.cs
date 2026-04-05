using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class UseConverterForTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure translation columns are text (not jsonb) since the EF
            // converter handles JSON serialization. This is idempotent — if
            // the columns are already text, the ALTERs are harmless no-ops.
            migrationBuilder.Sql("""
                ALTER TABLE "Products" ALTER COLUMN "NameTranslations" TYPE text;
                ALTER TABLE "Products" ALTER COLUMN "DescriptionTranslations" TYPE text;
                ALTER TABLE "BlogPosts" ALTER COLUMN "TitleTranslations" TYPE text;
                ALTER TABLE "BlogPosts" ALTER COLUMN "ContentTranslations" TYPE text;
                ALTER TABLE "BlogPosts" ALTER COLUMN "ExcerptTranslations" TYPE text;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No action — columns were text before and remain text
        }
    }
}
