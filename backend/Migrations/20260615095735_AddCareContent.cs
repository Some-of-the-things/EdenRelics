using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddCareContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareFabrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AlsoKnownAs = table.Column<string>(type: "text", nullable: false),
                    TargetKeywords = table.Column<string>(type: "text", nullable: false),
                    Intro = table.Column<string>(type: "text", nullable: false),
                    FiberContent = table.Column<string>(type: "text", nullable: false),
                    HowToIdentify = table.Column<string>(type: "text", nullable: false),
                    Washing = table.Column<string>(type: "text", nullable: false),
                    Drying = table.Column<string>(type: "text", nullable: false),
                    Ironing = table.Column<string>(type: "text", nullable: false),
                    Storing = table.Column<string>(type: "text", nullable: false),
                    VintageCautions = table.Column<string>(type: "text", nullable: false),
                    Dos = table.Column<string>(type: "text", nullable: false),
                    Donts = table.Column<string>(type: "text", nullable: false),
                    MetaTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MetaDescription = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewNotes = table.Column<string>(type: "text", nullable: false),
                    ReviewedBy = table.Column<string>(type: "text", nullable: true),
                    LastReviewedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareFabrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CareIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AlsoKnownAs = table.Column<string>(type: "text", nullable: false),
                    TargetKeywords = table.Column<string>(type: "text", nullable: false),
                    Causes = table.Column<string>(type: "text", nullable: false),
                    GeneralMethod = table.Column<string>(type: "text", nullable: false),
                    WhatNotToDo = table.Column<string>(type: "text", nullable: false),
                    WhenToSeeAPro = table.Column<string>(type: "text", nullable: false),
                    MetaTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MetaDescription = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewNotes = table.Column<string>(type: "text", nullable: false),
                    ReviewedBy = table.Column<string>(type: "text", nullable: true),
                    LastReviewedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareIssues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareFabrics_Slug",
                table: "CareFabrics",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareIssues_Slug",
                table: "CareIssues",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareFabrics");

            migrationBuilder.DropTable(
                name: "CareIssues");
        }
    }
}
