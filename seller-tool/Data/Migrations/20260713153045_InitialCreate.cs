using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdenRelics.SellerTool.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Garments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SellerRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Garments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoredRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Feature = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotBefore = table.Column<int>(type: "integer", nullable: true),
                    NotAfter = table.Column<int>(type: "integer", nullable: true),
                    Strength = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TransitionLagMonths = table.Column<int>(type: "integer", nullable: false),
                    SourceCitation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DateEstimates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Earliest = table.Column<int>(type: "integer", nullable: true),
                    Latest = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EvidenceChainJson = table.Column<string>(type: "text", nullable: false),
                    Confirmation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateEstimates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DateEstimates_Garments_GarmentId",
                        column: x => x.GarmentId,
                        principalTable: "Garments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Feature = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RawValue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ImageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Confirmation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConfirmedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceRecords_Garments_GarmentId",
                        column: x => x.GarmentId,
                        principalTable: "Garments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DateEstimates_GarmentId",
                table: "DateEstimates",
                column: "GarmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRecords_Feature",
                table: "EvidenceRecords",
                column: "Feature");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRecords_GarmentId",
                table: "EvidenceRecords",
                column: "GarmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Garments_OwnerId",
                table: "Garments",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredRules_Status_Feature",
                table: "StoredRules",
                columns: new[] { "Status", "Feature" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DateEstimates");

            migrationBuilder.DropTable(
                name: "EvidenceRecords");

            migrationBuilder.DropTable(
                name: "StoredRules");

            migrationBuilder.DropTable(
                name: "Garments");
        }
    }
}
