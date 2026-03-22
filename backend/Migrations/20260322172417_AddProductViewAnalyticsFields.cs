using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddProductViewAnalyticsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "ProductViews",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "ProductViews",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferrerUrl",
                table: "ProductViews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "ProductViews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmCampaign",
                table: "ProductViews",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmMedium",
                table: "ProductViews",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmSource",
                table: "ProductViews",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Channel",
                table: "ProductViews");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "ProductViews");

            migrationBuilder.DropColumn(
                name: "ReferrerUrl",
                table: "ProductViews");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "ProductViews");

            migrationBuilder.DropColumn(
                name: "UtmCampaign",
                table: "ProductViews");

            migrationBuilder.DropColumn(
                name: "UtmMedium",
                table: "ProductViews");

            migrationBuilder.DropColumn(
                name: "UtmSource",
                table: "ProductViews");
        }
    }
}
