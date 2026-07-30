using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddGarmentDatingArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EvidenceType = table.Column<int>(type: "integer", nullable: false),
                    TestKind = table.Column<int>(type: "integer", nullable: false),
                    TestValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BoundType = table.Column<int>(type: "integer", nullable: false),
                    BoundDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Strength = table.Column<int>(type: "integer", nullable: false),
                    SourceCitation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TrailingToleranceMonths = table.Column<int>(type: "integer", nullable: true),
                    ResearchNotes = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Garments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClaimedEraStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaimedEraEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Garments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Garments_Sellers_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Sellers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DatingAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Earliest = table.Column<DateOnly>(type: "date", nullable: true),
                    Latest = table.Column<DateOnly>(type: "date", nullable: true),
                    HasHardContradiction = table.Column<bool>(type: "boolean", nullable: false),
                    HasSoftContradiction = table.Column<bool>(type: "boolean", nullable: false),
                    ContradictsClaimedEra = table.Column<bool>(type: "boolean", nullable: false),
                    Confirmation = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatingAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatingAssessments_Garments_GarmentId",
                        column: x => x.GarmentId,
                        principalTable: "Garments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GarmentEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Confirmation = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarmentEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GarmentEvidence_Garments_GarmentId",
                        column: x => x.GarmentId,
                        principalTable: "Garments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DatingAssessmentSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RuleDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceCitation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceType = table.Column<int>(type: "integer", nullable: false),
                    EvidenceValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BoundType = table.Column<int>(type: "integer", nullable: false),
                    Strength = table.Column<int>(type: "integer", nullable: false),
                    BoundDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveBoundDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToleranceMonthsApplied = table.Column<int>(type: "integer", nullable: false),
                    AppliedToInterval = table.Column<bool>(type: "boolean", nullable: false),
                    ExclusionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatingAssessmentSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatingAssessmentSteps_DatingAssessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "DatingAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatingAssessments_GarmentId",
                table: "DatingAssessments",
                column: "GarmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DatingAssessmentSteps_AssessmentId",
                table: "DatingAssessmentSteps",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DatingRules_Code",
                table: "DatingRules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatingRules_Status_EvidenceType",
                table: "DatingRules",
                columns: new[] { "Status", "EvidenceType" });

            migrationBuilder.CreateIndex(
                name: "IX_GarmentEvidence_GarmentId_Type",
                table: "GarmentEvidence",
                columns: new[] { "GarmentId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Garments_SellerId",
                table: "Garments",
                column: "SellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatingAssessmentSteps");

            migrationBuilder.DropTable(
                name: "DatingRules");

            migrationBuilder.DropTable(
                name: "GarmentEvidence");

            migrationBuilder.DropTable(
                name: "DatingAssessments");

            migrationBuilder.DropTable(
                name: "Garments");
        }
    }
}
