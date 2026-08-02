using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdenRelics.SellerTool.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaptureStandardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ArchiveRightsGranted",
                table: "EvidenceRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "ByteSize",
                table: "EvidenceRecords",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaptureStandardVersion",
                table: "EvidenceRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "EvidenceRecords",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayImageKey",
                table: "EvidenceRecords",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "EvidenceRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slot",
                table: "EvidenceRecords",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "EvidenceRecords",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchiveRightsGranted",
                table: "EvidenceRecords");

            migrationBuilder.DropColumn(
                name: "ByteSize",
                table: "EvidenceRecords");

            migrationBuilder.DropColumn(
                name: "CaptureStandardVersion",
                table: "EvidenceRecords");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "EvidenceRecords");

            migrationBuilder.DropColumn(
                name: "DisplayImageKey",
                table: "EvidenceRecords");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "EvidenceRecords");

            migrationBuilder.DropColumn(
                name: "Slot",
                table: "EvidenceRecords");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "EvidenceRecords");
        }
    }
}
