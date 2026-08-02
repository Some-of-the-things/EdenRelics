using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdenRelics.SellerTool.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProvenanceMatchingAndTransitionGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Match",
                table: "StoredRules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Pattern",
                table: "StoredRules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provenance",
                table: "StoredRules",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResearchNotes",
                table: "StoredRules",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecId",
                table: "StoredRules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TransitionGroup",
                table: "StoredRules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoredTransitionGroups",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PeriodStart = table.Column<int>(type: "integer", nullable: false),
                    PeriodEnd = table.Column<int>(type: "integer", nullable: false),
                    TransitionLagMonths = table.Column<int>(type: "integer", nullable: false),
                    SourceCitation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Provenance = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredTransitionGroups", x => x.Code);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredTransitionGroups");

            migrationBuilder.DropColumn(
                name: "Match",
                table: "StoredRules");

            migrationBuilder.DropColumn(
                name: "Pattern",
                table: "StoredRules");

            migrationBuilder.DropColumn(
                name: "Provenance",
                table: "StoredRules");

            migrationBuilder.DropColumn(
                name: "ResearchNotes",
                table: "StoredRules");

            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "StoredRules");

            migrationBuilder.DropColumn(
                name: "TransitionGroup",
                table: "StoredRules");
        }
    }
}
