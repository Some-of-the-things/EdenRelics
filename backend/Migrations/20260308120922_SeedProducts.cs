using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class SeedProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Category", "Condition", "CreatedAtUtc", "Description", "Era", "ImageUrl", "InStock", "IsDeleted", "Name", "Price", "Size", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-0001-0000-0000-000000000001"), "70s", "good", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Flowing 1970s bohemian maxi dress with earthy floral print. Empire waist and angel sleeves in lightweight cotton gauze.", "1970s", "https://placehold.co/400x500/FF6347/FFF?text=Boho+Maxi+Dress", true, false, "Bohemian Maxi Dress", 195m, "10", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0002-0000-0000-000000000002"), "70s", "excellent", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Iconic 1970s wrap dress in a bold geometric print. Flattering silhouette with tie waist and flutter sleeves.", "1970s", "https://placehold.co/400x500/556B2F/FFF?text=Wrap+Dress", true, false, "Wrap Dress", 275m, "12", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0003-0000-0000-000000000003"), "80s", "excellent", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Bold 1980s power dress in electric blue with structured shoulders and nipped waist. Gold button details down the front.", "1980s", "https://placehold.co/400x500/191970/FFF?text=Power+Dress", true, false, "Power Shoulder Dress", 185m, "8", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0004-0000-0000-000000000004"), "80s", "good", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Dazzling 1980s sequin mini dress in hot pink. All-over sequin embellishment with dramatic puff sleeves.", "1980s", "https://placehold.co/400x500/8B0000/FFF?text=Sequin+Dress", true, false, "Sequin Party Dress", 220m, "6", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0005-0000-0000-000000000005"), "90s", "mint", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Minimalist 1990s silk slip dress in champagne. Bias-cut with delicate spaghetti straps and lace trim at the hem.", "1990s", "https://placehold.co/400x500/DAA520/FFF?text=Silk+Slip+Dress", true, false, "Silk Slip Dress", 210m, "8", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0006-0000-0000-000000000006"), "90s", "good", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Classic 1990s babydoll dress in dark floral. Oversized fit with empire waist and velvet ribbon trim.", "1990s", "https://placehold.co/400x500/2F4F4F/FFF?text=Babydoll+Dress", true, false, "Grunge Babydoll Dress", 145m, "14", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0007-0000-0000-000000000007"), "y2k", "excellent", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Early 2000s halter dress with butterfly print. Low-rise fit with handkerchief hem and rhinestone buckle detail.", "2000s", "https://placehold.co/400x500/FF69B4/FFF?text=Y2K+Halter", true, false, "Butterfly Halter Dress", 165m, "6", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0008-0000-0000-000000000008"), "y2k", "excellent", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Y2K velvet mini dress in deep plum. Scooped neckline with ruched sides and subtle stretch for a perfect fit.", "2000s", "https://placehold.co/400x500/8B4513/FFF?text=Velvet+Mini", true, false, "Velvet Mini Dress", 135m, "10", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0009-0000-0000-000000000009"), "modern", "mint", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Contemporary asymmetric midi dress in sage green. One-shoulder design with pleated skirt and clean modern lines.", "2020s", "https://placehold.co/400x500/556B2F/FFF?text=Asymmetric+Midi", true, false, "Asymmetric Midi Dress", 285m, "12", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0010-0000-0000-000000000010"), "modern", "mint", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Modern cut-out maxi dress in black. Strategic side cut-outs with a high neck and flowing skirt.", "2020s", "https://placehold.co/400x500/1C1C1C/FFF?text=Cut-Out+Maxi", true, false, "Cut-Out Maxi Dress", 320m, "16", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0001-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0002-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0003-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0004-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0005-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0006-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0007-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0008-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0009-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0010-0000-0000-000000000010"));
        }
    }
}
