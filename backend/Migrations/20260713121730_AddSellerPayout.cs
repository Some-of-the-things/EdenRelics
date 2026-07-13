using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerPayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SellerPayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Commission = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ReleaseAfterUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StripeTransferId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerPayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SellerPayouts_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerPayouts_Sellers_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Sellers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SellerPayouts_OrderId_SellerId",
                table: "SellerPayouts",
                columns: new[] { "OrderId", "SellerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerPayouts_SellerId",
                table: "SellerPayouts",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerPayouts_Status_ReleaseAfterUtc",
                table: "SellerPayouts",
                columns: new[] { "Status", "ReleaseAfterUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SellerPayouts");
        }
    }
}
