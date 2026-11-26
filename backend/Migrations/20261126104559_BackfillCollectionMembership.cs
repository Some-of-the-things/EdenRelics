using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <summary>
    /// Flags every piece listed in a curated collection (frontend collections.data.ts: Noir, The
    /// Wildflower Edit, Vintage Tartan) as a collection member, so it stays on its collection page,
    /// marked Sold, after it sells. The flag is normally set by "Approve &amp; publish" on the
    /// collection preview, but Wildflower was published before the flag existed and never re-run,
    /// so five sold dresses had vanished from it for signed-out visitors (2026-11-26).
    /// Idempotent; touches nothing but the flag. Down is a no-op: the earlier values aren't known,
    /// and the flag is harmless on a collection piece.
    /// </summary>
    public partial class BackfillCollectionMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Products"
                SET "InCollection" = TRUE
                WHERE NOT "InCollection"
                  AND "Sku" IN (
                    'ER-00008', 'ER-00037', 'ER-00039', 'ER-00050', 'ER-00078', 'ER-00081',
                    'ER-00099', 'ER-00100', 'ER-00101', 'ER-00102', 'ER-00103', 'ER-00109',
                    'ER-00114', 'ER-00115', 'ER-00116', 'ER-00117', 'ER-00118', 'ER-00119',
                    'ER-00120', 'ER-00121', 'ER-00123', 'ER-00124', 'ER-00149', 'ER-00207',
                    'ER-00208', 'ER-00272', 'ER-00273');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
