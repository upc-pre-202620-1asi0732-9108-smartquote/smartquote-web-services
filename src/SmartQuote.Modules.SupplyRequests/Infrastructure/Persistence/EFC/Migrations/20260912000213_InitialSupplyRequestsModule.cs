using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.SupplyRequests.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class InitialSupplyRequestsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "supply_requests");

            migrationBuilder.CreateTable(
                name: "purchase_requests",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_date = table.Column<DateOnly>(type: "date", nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "request_attachments",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_attachments_purchase_requests_purchase_request_id",
                        column: x => x.purchase_request_id,
                        principalSchema: "supply_requests",
                        principalTable: "purchase_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "request_notifications",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    new_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_notifications_purchase_requests_purchase_request_id",
                        column: x => x.purchase_request_id,
                        principalSchema: "supply_requests",
                        principalTable: "purchase_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "request_status_history",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_status_history_purchase_requests_purchase_request_id",
                        column: x => x.purchase_request_id,
                        principalSchema: "supply_requests",
                        principalTable: "purchase_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "requested_items",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requested_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_requested_items_purchase_requests_purchase_request_id",
                        column: x => x.purchase_request_id,
                        principalSchema: "supply_requests",
                        principalTable: "purchase_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "technical_requirements",
                schema: "supply_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    comparison_operator = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    expected_value = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    requested_item_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technical_requirements", x => x.id);
                    table.ForeignKey(
                        name: "fk_technical_requirements_requested_items_requested_item_id",
                        column: x => x.requested_item_id,
                        principalSchema: "supply_requests",
                        principalTable: "requested_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_request_attachments_purchase_request_id",
                schema: "supply_requests",
                table: "request_attachments",
                column: "purchase_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_notifications_purchase_request_id",
                schema: "supply_requests",
                table: "request_notifications",
                column: "purchase_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_status_history_purchase_request_id",
                schema: "supply_requests",
                table: "request_status_history",
                column: "purchase_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_requested_items_purchase_request_id_line_number",
                schema: "supply_requests",
                table: "requested_items",
                columns: new[] { "purchase_request_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_technical_requirements_requested_item_id",
                schema: "supply_requests",
                table: "technical_requirements",
                column: "requested_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_attachments",
                schema: "supply_requests");

            migrationBuilder.DropTable(
                name: "request_notifications",
                schema: "supply_requests");

            migrationBuilder.DropTable(
                name: "request_status_history",
                schema: "supply_requests");

            migrationBuilder.DropTable(
                name: "technical_requirements",
                schema: "supply_requests");

            migrationBuilder.DropTable(
                name: "requested_items",
                schema: "supply_requests");

            migrationBuilder.DropTable(
                name: "purchase_requests",
                schema: "supply_requests");
        }
    }
}
