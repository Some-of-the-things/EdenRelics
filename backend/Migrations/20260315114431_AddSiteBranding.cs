using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteBranding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BgPrimary = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BgSecondary = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BgCard = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BgDark = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TextPrimary = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TextSecondary = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TextMuted = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TextInverse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Accent = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AccentHover = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FontDisplay = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FontBody = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteBranding", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteBranding");
        }
    }
}
