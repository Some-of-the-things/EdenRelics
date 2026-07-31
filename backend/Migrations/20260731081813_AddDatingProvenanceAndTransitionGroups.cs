using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddDatingProvenanceAndTransitionGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Provenance",
                table: "DatingRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SpecId",
                table: "DatingRules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TransitionGroupCode",
                table: "DatingRules",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Provenance",
                table: "DatingAssessmentSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TransitionGroupCode",
                table: "DatingAssessmentSteps",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DatingTransitionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    TrailingToleranceMonths = table.Column<int>(type: "integer", nullable: true),
                    SourceCitation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Provenance = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatingTransitionGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatingTransitionGroups_Code",
                table: "DatingTransitionGroups",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatingTransitionGroups");

            migrationBuilder.DropColumn(
                name: "Provenance",
                table: "DatingRules");

            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "DatingRules");

            migrationBuilder.DropColumn(
                name: "TransitionGroupCode",
                table: "DatingRules");

            migrationBuilder.DropColumn(
                name: "Provenance",
                table: "DatingAssessmentSteps");

            migrationBuilder.DropColumn(
                name: "TransitionGroupCode",
                table: "DatingAssessmentSteps");
        }
    }
}
