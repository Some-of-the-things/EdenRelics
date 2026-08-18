using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdenRelics.SellerTool.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImageProvenanceAndZipOriginality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PhotographedAtLocal",
                table: "EvidenceRecords",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provenance",
                table: "EvidenceRecords",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                // Every row that already exists was written by the capture endpoint, which validates
                // against the standard - so LiveCapture is the truth for all of them. EF's own default
                // of "" is not a valid ImageProvenance and would fail to materialise on read.
                defaultValue: "LiveCapture");

            migrationBuilder.AddColumn<string>(
                name: "ZipOriginality",
                table: "EvidenceRecords",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRecords_GarmentId_Provenance",
                table: "EvidenceRecords",
                columns: new[] { "GarmentId", "Provenance" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EvidenceRecords_GarmentId_Provenance",
                table: "EvidenceRecords");

            migrationBuilder.DropColumn(
                name: "PhotographedAtLocal",
                table: "EvidenceRecords");

            migrationBuilder.DropColumn(
                name: "Provenance",
                table: "EvidenceRecords");

            migrationBuilder.DropColumn(
                name: "ZipOriginality",
                table: "EvidenceRecords");
        }
    }
}
