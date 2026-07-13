using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerTenancy : Migration
    {
        // Well-known house seller: Eden Relics' own first-party stock. All pre-marketplace
        // products/order-items are backfilled onto it so SellerId can be NOT NULL from day one.
        private const string HouseSellerId = "5e11e400-0000-0000-0000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- 1. Create Sellers and seed the house seller FIRST, so every product / order item
            //        can be backfilled onto a real seller before any FK constraint is enforced. ---
            migrationBuilder.CreateTable(
                name: "Sellers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Bio = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    IsHouse = table.Column<bool>(type: "boolean", nullable: false),
                    StripeConnectedAccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConnectOnboardingComplete = table.Column<bool>(type: "boolean", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sellers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sellers_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Sellers",
                columns: new[] { "Id", "ApprovalStatus", "Bio", "BusinessName", "CommissionRate", "ConnectOnboardingComplete", "ContactEmail", "CreatedAtUtc", "IsDeleted", "IsHouse", "LogoUrl", "OwnerUserId", "Slug", "StripeConnectedAccountId", "UpdatedAtUtc" },
                values: new object[] { new Guid(HouseSellerId), 1, null, "Eden Relics", null, false, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, true, null, null, "eden-relics", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            // --- 2. Drop the old global SKU unique index (replaced by a per-seller composite). ---
            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            // --- 3. Add SellerId columns. Transactions stays nullable by design (platform rows have
            //        no seller). Products/OrderItems are added NULLABLE so existing rows can be
            //        backfilled (step 4) before becoming NOT NULL + FK-constrained (step 5). ---
            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "OrderItems",
                type: "uuid",
                nullable: true);

            // --- 4. Backfill every existing product and order item onto the house seller. ---
            migrationBuilder.Sql(
                $"UPDATE \"Products\" SET \"SellerId\" = '{HouseSellerId}' WHERE \"SellerId\" IS NULL;");
            migrationBuilder.Sql(
                "UPDATE \"OrderItems\" AS oi SET \"SellerId\" = p.\"SellerId\" FROM \"Products\" AS p " +
                "WHERE oi.\"ProductId\" = p.\"Id\" AND oi.\"SellerId\" IS NULL;");
            migrationBuilder.Sql(
                $"UPDATE \"OrderItems\" SET \"SellerId\" = '{HouseSellerId}' WHERE \"SellerId\" IS NULL;");

            // --- 5. Every row is now populated: make the columns NOT NULL to match the model. ---
            migrationBuilder.AlterColumn<Guid>(
                name: "SellerId",
                table: "Products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SellerId",
                table: "OrderItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // --- 6. Indexes and foreign keys. ---
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SellerId",
                table: "Transactions",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SellerId",
                table: "Products",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SellerId_Sku",
                table: "Products",
                columns: new[] { "SellerId", "Sku" },
                unique: true,
                filter: "\"Sku\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_SellerId",
                table: "OrderItems",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sellers_OwnerUserId",
                table: "Sellers",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sellers_Slug",
                table: "Sellers",
                column: "Slug",
                unique: true,
                filter: "\"Slug\" <> ''");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Sellers_SellerId",
                table: "OrderItems",
                column: "SellerId",
                principalTable: "Sellers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Sellers_SellerId",
                table: "Products",
                column: "SellerId",
                principalTable: "Sellers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Sellers_SellerId",
                table: "Transactions",
                column: "SellerId",
                principalTable: "Sellers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Sellers_SellerId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Sellers_SellerId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Sellers_SellerId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "Sellers");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_SellerId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Products_SellerId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SellerId_Sku",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_SellerId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "OrderItems");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true,
                filter: "\"Sku\" <> ''");
        }
    }
}
