using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.PurchaseOrdering.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class AddAtomicPurchaseAwards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_purchase_orders_source_purchase_request_id",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "ix_purchase_orders_source_simulation_run_id",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "source_quotation_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "award_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "award_supplier_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                schema: "purchase_ordering",
                table: "purchase_order_lines",
                type: "numeric(28,12)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AddColumn<string>(
                name: "original_currency",
                schema: "purchase_ordering",
                table: "purchase_order_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "original_unit_price",
                schema: "purchase_ordering",
                table: "purchase_order_lines",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_quotation_id",
                schema: "purchase_ordering",
                table: "purchase_order_lines",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ordering_outbox",
                schema: "purchase_ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ordering_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_awards",
                schema: "purchase_ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    simulation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    input_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_pen = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    budget_version = table.Column<long>(type: "bigint", nullable: true),
                    budget_amount_pen = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    budget_margin_pen = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    exchange_rate_source = table.Column<string>(type: "text", nullable: true),
                    exchange_rate_type = table.Column<string>(type: "text", nullable: true),
                    rate_published_on = table.Column<DateOnly>(type: "date", nullable: true),
                    rate_retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_awards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_award_allocations",
                schema: "purchase_ordering",
                columns: table => new
                {
                    requested_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    award_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_of_measure = table.Column<string>(type: "text", nullable: false),
                    original_currency = table.Column<string>(type: "text", nullable: false),
                    original_unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price_pen = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    amount_pen = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_award_allocations", x => new { x.award_id, x.requested_item_id });
                    table.ForeignKey(
                        name: "fk_purchase_award_allocations_purchase_awards_award_id",
                        column: x => x.award_id,
                        principalSchema: "purchase_ordering",
                        principalTable: "purchase_awards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_award_id_award_supplier_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                columns: new[] { "award_id", "award_supplier_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_source_purchase_request_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                column: "source_purchase_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_source_simulation_run_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                column: "source_simulation_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_ordering_outbox_processed_at",
                schema: "purchase_ordering",
                table: "ordering_outbox",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_awards_idempotency_key",
                schema: "purchase_ordering",
                table: "purchase_awards",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_awards_request_id",
                schema: "purchase_ordering",
                table: "purchase_awards",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_awards_simulation_run_id",
                schema: "purchase_ordering",
                table: "purchase_awards",
                column: "simulation_run_id",
                unique: true);

            // Backfill before replacing the single-order invariant with a single-award invariant.
            // IDs and numbers of existing orders remain untouched. Do not invent unknown USD rates.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM purchase_ordering.purchase_orders o
                        WHERE o.currency <> 'PEN' AND o.policy_total_pen IS NULL AND NOT EXISTS
                        (SELECT 1 FROM evaluation_simulation.simulation_exchange_rate_snapshots r WHERE r.simulation_run_id = o.source_simulation_run_id))
                    THEN RAISE EXCEPTION 'Historical non-PEN orders need their original conversion evidence reconciled before award migration.'; END IF;
                END $$;
                INSERT INTO purchase_ordering.purchase_awards
                    (id, request_id, simulation_run_id, mode, input_fingerprint, idempotency_key, request_hash,
                     approved_by, approved_at, total_pen, budget_version, budget_amount_pen, budget_margin_pen,
                     exchange_rate, exchange_rate_source, exchange_rate_type, rate_published_on, rate_retrieved_at)
                SELECT o.id, o.source_purchase_request_id, o.source_simulation_run_id, 'SingleSupplier', o.input_fingerprint,
                    o.idempotency_key, o.input_fingerprint, o.approved_by, o.approved_at,
                    COALESCE(o.policy_total_pen, (SELECT SUM(l.quantity * l.unit_price) FROM purchase_ordering.purchase_order_lines l WHERE l.purchase_order_id = o.id)
                        * CASE WHEN o.currency = 'PEN' THEN 1 ELSE (SELECT r.rate FROM evaluation_simulation.simulation_exchange_rate_snapshots r WHERE r.simulation_run_id = o.source_simulation_run_id) END),
                    o.policy_budget_version, o.policy_budget_amount_pen, o.policy_margin_pen,
                    o.policy_exchange_rate, o.policy_exchange_rate_source, o.policy_rate_type, o.policy_rate_published_on, o.policy_rate_retrieved_at
                FROM purchase_ordering.purchase_orders o;
                UPDATE purchase_ordering.purchase_orders SET award_id = id,
                    award_supplier_id = COALESCE(policy_supplier_account_id,
                        (SELECT s.id FROM purchase_ordering.supplier_accounts s WHERE s.tax_identifier = supplier_tax_identifier));
                UPDATE purchase_ordering.purchase_order_lines l SET source_quotation_id = o.source_quotation_id::text,
                    original_currency = o.currency, original_unit_price = l.unit_price
                FROM purchase_ordering.purchase_orders o WHERE o.id = l.purchase_order_id;
                UPDATE purchase_ordering.purchase_awards a SET exchange_rate = r.rate, exchange_rate_source = r.source,
                    exchange_rate_type = r.rate_type, rate_published_on = r.published_on, rate_retrieved_at = r.retrieved_at
                FROM evaluation_simulation.simulation_exchange_rate_snapshots r, purchase_ordering.purchase_orders o
                WHERE a.id = o.id AND o.currency = 'USD' AND a.exchange_rate IS NULL AND r.simulation_run_id = a.simulation_run_id;
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_purchase_orders_purchase_awards_award_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                column: "award_id",
                principalSchema: "purchase_ordering",
                principalTable: "purchase_awards",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM purchase_ordering.purchase_awards WHERE mode = 'DistributedByItem')
                    THEN RAISE EXCEPTION 'Distributed awards exist. Restore a backup rather than discard multi-order provenance.'; END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "fk_purchase_orders_purchase_awards_award_id",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropTable(
                name: "ordering_outbox",
                schema: "purchase_ordering");

            migrationBuilder.DropTable(
                name: "purchase_award_allocations",
                schema: "purchase_ordering");

            migrationBuilder.DropTable(
                name: "purchase_awards",
                schema: "purchase_ordering");

            migrationBuilder.DropIndex(
                name: "ix_purchase_orders_award_id_award_supplier_id",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "ix_purchase_orders_source_purchase_request_id",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "ix_purchase_orders_source_simulation_run_id",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "award_id",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "award_supplier_id",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "original_currency",
                schema: "purchase_ordering",
                table: "purchase_order_lines");

            migrationBuilder.DropColumn(
                name: "original_unit_price",
                schema: "purchase_ordering",
                table: "purchase_order_lines");

            migrationBuilder.DropColumn(
                name: "source_quotation_id",
                schema: "purchase_ordering",
                table: "purchase_order_lines");

            migrationBuilder.AlterColumn<Guid>(
                name: "source_quotation_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                schema: "purchase_ordering",
                table: "purchase_order_lines",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,12)");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_source_purchase_request_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                column: "source_purchase_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_source_simulation_run_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                column: "source_simulation_run_id",
                unique: true);
        }
    }
}
