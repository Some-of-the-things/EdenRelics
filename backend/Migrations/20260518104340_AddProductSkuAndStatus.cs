using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSkuAndStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "Products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 1); // Default Live so untouched rows stay visible.

            // Copy data from InStock before we drop the column. true -> Live (1), false -> Sold (2).
            migrationBuilder.Sql(@"
                UPDATE ""Products""
                SET ""Status"" = CASE WHEN ""InStock"" THEN 1 ELSE 2 END;
            ");

            // Deterministic SKU + Status for the 10 seeded products.
            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0001-0000-0000-000000000001"),
                columns: new[] { "Sku", "Status" },
                values: new object[] { "ER-00001", 1 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0002-0000-0000-000000000002"),
                columns: new[] { "Sku", "Status" },
                values: new object[] { "ER-00002", 1 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0003-0000-0000-000000000003"),
                columns: new[] { "Sku", "Status" },
                values: new object[] { "ER-00003", 1 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0004-0000-0000-000000000004"),
                columns: new[] { "Sku", "Status" },
                values: new object[] { "ER-00004", 1 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0005-0000-0000-000000000005"),
                columns: new[] { "Sku", "Status" },
                values: new object[] { "ER-00005", 1 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0006-0000-0000-000000000006"),
                columns: new[] { "Sku", "Status" },
                values: new object[] { "ER-00006", 1 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0007-0000-0000-000000000007"),
                columns: new[] { "Sku", "Status" },
                values: new object[] { "ER-00007", 1 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0008-0000-0000-000000000008"),
                columns: new[] { "Sku", "Status" },
                values: new object[] { "ER-00008", 1 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0009-0000-0000-000000000009"),
                columns: new[] { "Sku", "Status" },
                values: new object[] { "ER-00009", 1 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0010-0000-0000-000000000010"),
                columns: new[] { "Sku", "Status" },
                values: new object[] { "ER-00010", 1 });

            // Backfill any non-seeded product still missing a SKU, starting one above the
            // highest existing ER-NNNNN value. Ordered by CreatedAtUtc + Id for determinism.
            migrationBuilder.Sql(@"
                WITH base AS (
                    SELECT COALESCE(MAX(CAST(SUBSTRING(""Sku"" FROM '^ER-(\d+)$') AS INTEGER)), 0) AS max_seq
                    FROM ""Products""
                    WHERE ""Sku"" ~ '^ER-\d+$'
                ),
                numbered AS (
                    SELECT ""Id"",
                           ROW_NUMBER() OVER (ORDER BY ""CreatedAtUtc"", ""Id"") AS rn
                    FROM ""Products""
                    WHERE ""Sku"" = ''
                )
                UPDATE ""Products"" p
                SET ""Sku"" = 'ER-' || LPAD((numbered.rn + base.max_seq)::text, 5, '0')
                FROM numbered, base
                WHERE p.""Id"" = numbered.""Id"";
            ");

            migrationBuilder.DropColumn(
                name: "InStock",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true,
                filter: "\"Sku\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InStock",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
                UPDATE ""Products""
                SET ""InStock"" = (""Status"" = 1);
            ");

            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Products");
        }
    }
}
