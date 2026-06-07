using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eden_Relics_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarObligationsAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiabilityObligations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FiledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmissionReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OwedAmountMinor = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAmountMinor = table.Column<long>(type: "bigint", nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiabilityObligations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperatorReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Recurrence = table.Column<int>(type: "integer", nullable: false),
                    NotifyEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    LastNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    LinkedObligationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorReminders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiabilityObligations_DueDate",
                table: "LiabilityObligations",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_LiabilityObligations_Kind_PeriodEnd",
                table: "LiabilityObligations",
                columns: new[] { "Kind", "PeriodEnd" },
                unique: true,
                filter: "\"Kind\" <> 99");

            migrationBuilder.CreateIndex(
                name: "IX_LiabilityObligations_ScheduledFor",
                table: "LiabilityObligations",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorReminders_IsActive_DueAt",
                table: "OperatorReminders",
                columns: new[] { "IsActive", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorReminders_LinkedObligationId",
                table: "OperatorReminders",
                column: "LinkedObligationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiabilityObligations");

            migrationBuilder.DropTable(
                name: "OperatorReminders");
        }
    }
}
