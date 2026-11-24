using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddSignupOfferAndEarlyAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EarlyAccessUntilUtc",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountCode",
                table: "Orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewReminderSentAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BouncedAtUtc",
                table: "NewsletterDeliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClickedAtUtc",
                table: "NewsletterDeliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAtUtc",
                table: "NewsletterDeliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderId",
                table: "NewsletterDeliveries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscountCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    MinOrder = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ExcludeReduced = table.Column<bool>(type: "boolean", nullable: false),
                    ExcludeGiftOrders = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountCodes", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0001-0000-0000-000000000001"),
                column: "EarlyAccessUntilUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0002-0000-0000-000000000002"),
                column: "EarlyAccessUntilUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0003-0000-0000-000000000003"),
                column: "EarlyAccessUntilUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0004-0000-0000-000000000004"),
                column: "EarlyAccessUntilUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0005-0000-0000-000000000005"),
                column: "EarlyAccessUntilUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0006-0000-0000-000000000006"),
                column: "EarlyAccessUntilUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0007-0000-0000-000000000007"),
                column: "EarlyAccessUntilUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0008-0000-0000-000000000008"),
                column: "EarlyAccessUntilUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0009-0000-0000-000000000009"),
                column: "EarlyAccessUntilUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0010-0000-0000-000000000010"),
                column: "EarlyAccessUntilUtc",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterDeliveries_ProviderId",
                table: "NewsletterDeliveries",
                column: "ProviderId",
                filter: "\"ProviderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_Code",
                table: "DiscountCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_Email",
                table: "DiscountCodes",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscountCodes");

            migrationBuilder.DropIndex(
                name: "IX_NewsletterDeliveries_ProviderId",
                table: "NewsletterDeliveries");

            migrationBuilder.DropColumn(
                name: "EarlyAccessUntilUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReviewReminderSentAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BouncedAtUtc",
                table: "NewsletterDeliveries");

            migrationBuilder.DropColumn(
                name: "ClickedAtUtc",
                table: "NewsletterDeliveries");

            migrationBuilder.DropColumn(
                name: "OpenedAtUtc",
                table: "NewsletterDeliveries");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "NewsletterDeliveries");
        }
    }
}
