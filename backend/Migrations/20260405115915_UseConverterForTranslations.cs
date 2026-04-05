using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class UseConverterForTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NameTranslations",
                table: "Products",
                type: "text",
                nullable: false,
                oldClrType: typeof(Dictionary<string, string>),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionTranslations",
                table: "Products",
                type: "text",
                nullable: false,
                oldClrType: typeof(Dictionary<string, string>),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "TitleTranslations",
                table: "BlogPosts",
                type: "text",
                nullable: false,
                oldClrType: typeof(Dictionary<string, string>),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "ExcerptTranslations",
                table: "BlogPosts",
                type: "text",
                nullable: false,
                oldClrType: typeof(Dictionary<string, string>),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "ContentTranslations",
                table: "BlogPosts",
                type: "text",
                nullable: false,
                oldClrType: typeof(Dictionary<string, string>),
                oldType: "jsonb");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0001-0000-0000-000000000001"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { "{}", "{}" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0002-0000-0000-000000000002"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { "{}", "{}" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0003-0000-0000-000000000003"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { "{}", "{}" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0004-0000-0000-000000000004"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { "{}", "{}" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0005-0000-0000-000000000005"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { "{}", "{}" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0006-0000-0000-000000000006"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { "{}", "{}" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0007-0000-0000-000000000007"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { "{}", "{}" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0008-0000-0000-000000000008"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { "{}", "{}" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0009-0000-0000-000000000009"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { "{}", "{}" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0010-0000-0000-000000000010"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { "{}", "{}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Dictionary<string, string>>(
                name: "NameTranslations",
                table: "Products",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Dictionary<string, string>>(
                name: "DescriptionTranslations",
                table: "Products",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Dictionary<string, string>>(
                name: "TitleTranslations",
                table: "BlogPosts",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Dictionary<string, string>>(
                name: "ExcerptTranslations",
                table: "BlogPosts",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Dictionary<string, string>>(
                name: "ContentTranslations",
                table: "BlogPosts",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0001-0000-0000-000000000001"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { new Dictionary<string, string>(), new Dictionary<string, string>() });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0002-0000-0000-000000000002"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { new Dictionary<string, string>(), new Dictionary<string, string>() });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0003-0000-0000-000000000003"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { new Dictionary<string, string>(), new Dictionary<string, string>() });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0004-0000-0000-000000000004"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { new Dictionary<string, string>(), new Dictionary<string, string>() });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0005-0000-0000-000000000005"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { new Dictionary<string, string>(), new Dictionary<string, string>() });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0006-0000-0000-000000000006"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { new Dictionary<string, string>(), new Dictionary<string, string>() });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0007-0000-0000-000000000007"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { new Dictionary<string, string>(), new Dictionary<string, string>() });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0008-0000-0000-000000000008"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { new Dictionary<string, string>(), new Dictionary<string, string>() });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0009-0000-0000-000000000009"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { new Dictionary<string, string>(), new Dictionary<string, string>() });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0010-0000-0000-000000000010"),
                columns: new[] { "DescriptionTranslations", "NameTranslations" },
                values: new object[] { new Dictionary<string, string>(), new Dictionary<string, string>() });
        }
    }
}
