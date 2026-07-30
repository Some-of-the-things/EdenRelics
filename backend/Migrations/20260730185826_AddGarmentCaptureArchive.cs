using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddGarmentCaptureArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CaptureId",
                table: "GarmentEvidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GarmentCaptures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    ArchiveUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    ArchiveRightsGranted = table.Column<bool>(type: "boolean", nullable: false),
                    ArchiveTermsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarmentCaptures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GarmentCaptures_Garments_GarmentId",
                        column: x => x.GarmentId,
                        principalTable: "Garments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GarmentEvidence_CaptureId",
                table: "GarmentEvidence",
                column: "CaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_GarmentCaptures_GarmentId_Slot",
                table: "GarmentCaptures",
                columns: new[] { "GarmentId", "Slot" });

            migrationBuilder.AddForeignKey(
                name: "FK_GarmentEvidence_GarmentCaptures_CaptureId",
                table: "GarmentEvidence",
                column: "CaptureId",
                principalTable: "GarmentCaptures",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GarmentEvidence_GarmentCaptures_CaptureId",
                table: "GarmentEvidence");

            migrationBuilder.DropTable(
                name: "GarmentCaptures");

            migrationBuilder.DropIndex(
                name: "IX_GarmentEvidence_CaptureId",
                table: "GarmentEvidence");

            migrationBuilder.DropColumn(
                name: "CaptureId",
                table: "GarmentEvidence");
        }
    }
}
