using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.PurchaseOrdering.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class InitialPurchaseOrderingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "purchase_ordering");

            migrationBuilder.CreateSequence(
                name: "order_number_seq",
                schema: "purchase_ordering");

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                schema: "purchase_ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_simulation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_fingerprint = table.Column<string>(type: "char(64)", nullable: false),
                    supplier_reference_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    supplier_business_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_tax_identifier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    delivery_lead_time_days = table.Column<int>(type: "integer", nullable: false),
                    delivery_conditions = table.Column<string>(type: "text", nullable: false),
                    delivery_destination = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                schema: "purchase_ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    source_quotation_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_requested_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_order_lines_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalSchema: "purchase_ordering",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_purchase_order_id",
                schema: "purchase_ordering",
                table: "purchase_order_lines",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_idempotency_key",
                schema: "purchase_ordering",
                table: "purchase_orders",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_order_number",
                schema: "purchase_ordering",
                table: "purchase_orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_source_simulation_run_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                column: "source_simulation_run_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_order_lines",
                schema: "purchase_ordering");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "purchase_ordering");

            migrationBuilder.DropSequence(
                name: "order_number_seq",
                schema: "purchase_ordering");
        }
    }
}
