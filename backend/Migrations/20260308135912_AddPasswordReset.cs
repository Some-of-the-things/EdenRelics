using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine1",
                table: "Users",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine2",
                table: "Users",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCity",
                table: "Users",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCountry",
                table: "Users",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCounty",
                table: "Users",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingPostcode",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddressLine1",
                table: "Users",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddressLine2",
                table: "Users",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCity",
                table: "Users",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCountry",
                table: "Users",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCounty",
                table: "Users",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPostcode",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                table: "Users",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetTokenExpiresUtc",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCardBrand",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentCardExpiryMonth",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentCardExpiryYear",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCardLast4",
                table: "Users",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCardholderName",
                table: "Users",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingAddressLine1",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BillingAddressLine2",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BillingCity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BillingCountry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BillingCounty",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BillingPostcode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeliveryAddressLine1",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeliveryAddressLine2",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeliveryCity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeliveryCountry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeliveryCounty",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeliveryPostcode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenExpiresUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PaymentCardBrand",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PaymentCardExpiryMonth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PaymentCardExpiryYear",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PaymentCardLast4",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PaymentCardholderName",
                table: "Users");
        }
    }
}
