using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderShippingAndAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillAddressLine1",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillAddressLine2",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillCity",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillCountry",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillCounty",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillPostcode",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipAddressLine1",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipAddressLine2",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipCity",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipCountry",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipCounty",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipPostcode",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingCost",
                table: "Orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShippingMethod",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillAddressLine1",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BillAddressLine2",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BillCity",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BillCountry",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BillCounty",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BillPostcode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShipAddressLine1",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShipAddressLine2",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShipCity",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShipCountry",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShipCounty",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShipPostcode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingCost",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingMethod",
                table: "Orders");
        }
    }
}
