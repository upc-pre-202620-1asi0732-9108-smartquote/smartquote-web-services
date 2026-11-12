using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.SupplyRequests.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestCollaborationRecurrenceAndAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assigned_analyst_id",
                schema: "supply_requests",
                table: "purchase_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "schedule_occurrence_id",
                schema: "supply_requests",
                table: "purchase_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "technical_revision",
                schema: "supply_requests",
                table: "purchase_requests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "recurring_request_schedules",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    interval_days = table.Column<int>(type: "integer", nullable: false),
                    required_date_offset_days = table.Column<int>(type: "integer", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    items = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    next_occurrence_date = table.Column<DateOnly>(type: "date", nullable: false),
                    next_occurrence_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_request_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "request_assignment_history",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_analyst_id = table.Column<Guid>(type: "uuid", nullable: true),
                    analyst_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_assignment_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_assignment_history_purchase_requests_purchase_reque~",
                        column: x => x.purchase_request_id,
                        principalSchema: "supply_requests",
                        principalTable: "purchase_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "requirement_revisions",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clarification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technical_revision = table.Column<long>(type: "bigint", nullable: false),
                    previous_name = table.Column<string>(type: "text", nullable: false),
                    previous_operator = table.Column<string>(type: "text", nullable: false),
                    previous_expected_value = table.Column<string>(type: "text", nullable: false),
                    previous_unit_of_measure = table.Column<string>(type: "text", nullable: false),
                    previous_is_mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    current_name = table.Column<string>(type: "text", nullable: false),
                    current_operator = table.Column<string>(type: "text", nullable: false),
                    current_expected_value = table.Column<string>(type: "text", nullable: false),
                    current_unit_of_measure = table.Column<string>(type: "text", nullable: false),
                    current_is_mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requirement_revisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_requirement_revisions_purchase_requests_purchase_request_id",
                        column: x => x.purchase_request_id,
                        principalSchema: "supply_requests",
                        principalTable: "purchase_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "technical_clarifications",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    notice_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    answered_by = table.Column<Guid>(type: "uuid", nullable: true),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source_technical_revision = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technical_clarifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_technical_clarifications_purchase_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "supply_requests",
                        principalTable: "purchase_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schedule_occurrences",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_occurrences", x => x.id);
                    table.ForeignKey(
                        name: "fk_schedule_occurrences_recurring_request_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalSchema: "supply_requests",
                        principalTable: "recurring_request_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_requests_assigned_analyst_id_status",
                schema: "supply_requests",
                table: "purchase_requests",
                columns: new[] { "assigned_analyst_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_requests_schedule_occurrence_id",
                schema: "supply_requests",
                table: "purchase_requests",
                column: "schedule_occurrence_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recurring_request_schedules_is_active_next_occurrence_at",
                schema: "supply_requests",
                table: "recurring_request_schedules",
                columns: new[] { "is_active", "next_occurrence_at" });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_request_schedules_owner_id",
                schema: "supply_requests",
                table: "recurring_request_schedules",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_assignment_history_purchase_request_id",
                schema: "supply_requests",
                table: "request_assignment_history",
                column: "purchase_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_requirement_revisions_purchase_request_id_technical_revision",
                schema: "supply_requests",
                table: "requirement_revisions",
                columns: new[] { "purchase_request_id", "technical_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_occurrences_request_id",
                schema: "supply_requests",
                table: "schedule_occurrences",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_occurrences_schedule_id_scheduled_date",
                schema: "supply_requests",
                table: "schedule_occurrences",
                columns: new[] { "schedule_id", "scheduled_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_technical_clarifications_request_id",
                schema: "supply_requests",
                table: "technical_clarifications",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_technical_clarifications_specialist_id_status",
                schema: "supply_requests",
                table: "technical_clarifications",
                columns: new[] { "specialist_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_assignment_history",
                schema: "supply_requests");

            migrationBuilder.DropTable(
                name: "requirement_revisions",
                schema: "supply_requests");

            migrationBuilder.DropTable(
                name: "schedule_occurrences",
                schema: "supply_requests");

            migrationBuilder.DropTable(
                name: "technical_clarifications",
                schema: "supply_requests");

            migrationBuilder.DropTable(
                name: "recurring_request_schedules",
                schema: "supply_requests");

            migrationBuilder.DropIndex(
                name: "ix_purchase_requests_assigned_analyst_id_status",
                schema: "supply_requests",
                table: "purchase_requests");

            migrationBuilder.DropIndex(
                name: "ix_purchase_requests_schedule_occurrence_id",
                schema: "supply_requests",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "assigned_analyst_id",
                schema: "supply_requests",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "schedule_occurrence_id",
                schema: "supply_requests",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "technical_revision",
                schema: "supply_requests",
                table: "purchase_requests");
        }
    }
}
