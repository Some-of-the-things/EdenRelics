using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class RestoreJsonbColumnTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The AddTranslationColumns migration created these as 'text' columns.
            // Npgsql needs them as 'jsonb' for native Dictionary<string,string> mapping.
            // Drop defaults first, alter type, then restore defaults.
            string[] productCols = ["NameTranslations", "DescriptionTranslations"];
            string[] blogCols = ["TitleTranslations", "ContentTranslations", "ExcerptTranslations"];

            foreach (string col in productCols)
            {
                migrationBuilder.Sql($"ALTER TABLE \"Products\" ALTER COLUMN \"{col}\" DROP DEFAULT");
                migrationBuilder.Sql($"ALTER TABLE \"Products\" ALTER COLUMN \"{col}\" TYPE jsonb USING \"{col}\"::jsonb");
                migrationBuilder.Sql($"ALTER TABLE \"Products\" ALTER COLUMN \"{col}\" SET DEFAULT '{{}}'::jsonb");
            }

            foreach (string col in blogCols)
            {
                migrationBuilder.Sql($"ALTER TABLE \"BlogPosts\" ALTER COLUMN \"{col}\" DROP DEFAULT");
                migrationBuilder.Sql($"ALTER TABLE \"BlogPosts\" ALTER COLUMN \"{col}\" TYPE jsonb USING \"{col}\"::jsonb");
                migrationBuilder.Sql($"ALTER TABLE \"BlogPosts\" ALTER COLUMN \"{col}\" SET DEFAULT '{{}}'::jsonb");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            string[] productCols = ["NameTranslations", "DescriptionTranslations"];
            string[] blogCols = ["TitleTranslations", "ContentTranslations", "ExcerptTranslations"];

            foreach (string col in productCols)
            {
                migrationBuilder.Sql($"ALTER TABLE \"Products\" ALTER COLUMN \"{col}\" DROP DEFAULT");
                migrationBuilder.Sql($"ALTER TABLE \"Products\" ALTER COLUMN \"{col}\" TYPE text");
            }

            foreach (string col in blogCols)
            {
                migrationBuilder.Sql($"ALTER TABLE \"BlogPosts\" ALTER COLUMN \"{col}\" DROP DEFAULT");
                migrationBuilder.Sql($"ALTER TABLE \"BlogPosts\" ALTER COLUMN \"{col}\" TYPE text");
            }
        }
    }
}
