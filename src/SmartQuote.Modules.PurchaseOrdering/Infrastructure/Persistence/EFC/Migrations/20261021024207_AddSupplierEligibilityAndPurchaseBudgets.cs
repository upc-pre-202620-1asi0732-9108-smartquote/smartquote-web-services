using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.PurchaseOrdering.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierEligibilityAndPurchaseBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "policy_budget_amount_pen",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "policy_budget_version",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "policy_exchange_rate",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "numeric(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "policy_exchange_rate_source",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "policy_margin_pen",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "numeric(28,8)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "policy_rate_published_on",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "policy_rate_retrieved_at",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "policy_rate_type",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "policy_supplier_account_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "policy_supplier_eligibility_version",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "policy_total_pen",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "numeric(28,8)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "purchase_budgets",
                schema: "purchase_ordering",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_pen = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_budgets", x => x.request_id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_accounts",
                schema: "purchase_ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_identifier = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    business_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_budget_revisions",
                schema: "purchase_ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    amount_pen = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    authorized_by = table.Column<Guid>(type: "uuid", nullable: false),
                    authorized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_budget_revisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_budget_revisions_purchase_budgets_request_id",
                        column: x => x.request_id,
                        principalSchema: "purchase_ordering",
                        principalTable: "purchase_budgets",
                        principalColumn: "request_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_eligibility_history",
                schema: "purchase_ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_status = table.Column<string>(type: "text", nullable: false),
                    new_status = table.Column<string>(type: "text", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_eligibility_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_eligibility_history_supplier_accounts_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "purchase_ordering",
                        principalTable: "supplier_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_budget_revisions_request_id_version",
                schema: "purchase_ordering",
                table: "purchase_budget_revisions",
                columns: new[] { "request_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_accounts_tax_identifier",
                schema: "purchase_ordering",
                table: "supplier_accounts",
                column: "tax_identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_eligibility_history_supplier_id_version",
                schema: "purchase_ordering",
                table: "supplier_eligibility_history",
                columns: new[] { "supplier_id", "version" },
                unique: true);

            // Reconcile identities, not authorizations. Historical suppliers start Pending; old orders remain intact.
            migrationBuilder.Sql("""
                INSERT INTO purchase_ordering.supplier_accounts
                    (id, tax_identifier, business_name, status, version, created_at)
                SELECT gen_random_uuid(), tax_identifier, MAX(business_name), 'Pending', 1, CURRENT_TIMESTAMP
                FROM (
                    SELECT trim(supplier_tax_identifier) AS tax_identifier, supplier_business_name AS business_name
                    FROM purchase_ordering.purchase_orders
                    WHERE trim(supplier_tax_identifier) ~ '^[0-9]{11}$'
                      AND length(trim(supplier_business_name)) BETWEEN 1 AND 200
                ) AS source
                GROUP BY tax_identifier ON CONFLICT (tax_identifier) DO NOTHING;

                DO $$ BEGIN
                    IF to_regclass('quotation_intake.poultry_quotes') IS NOT NULL THEN
                        INSERT INTO purchase_ordering.supplier_accounts
                            (id, tax_identifier, business_name, status, version, created_at)
                        SELECT gen_random_uuid(), trim(supplier_tax_identifier), MAX(supplier_business_name), 'Pending', 1, CURRENT_TIMESTAMP
                        FROM quotation_intake.poultry_quotes
                        WHERE trim(supplier_tax_identifier) ~ '^[0-9]{11}$'
                          AND length(trim(supplier_business_name)) BETWEEN 1 AND 200
                        GROUP BY trim(supplier_tax_identifier)
                        ON CONFLICT (tax_identifier) DO NOTHING;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_budget_revisions",
                schema: "purchase_ordering");

            migrationBuilder.DropTable(
                name: "supplier_eligibility_history",
                schema: "purchase_ordering");

            migrationBuilder.DropTable(
                name: "purchase_budgets",
                schema: "purchase_ordering");

            migrationBuilder.DropTable(
                name: "supplier_accounts",
                schema: "purchase_ordering");

            migrationBuilder.DropColumn(
                name: "policy_budget_amount_pen",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "policy_budget_version",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "policy_exchange_rate",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "policy_exchange_rate_source",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "policy_margin_pen",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "policy_rate_published_on",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "policy_rate_retrieved_at",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "policy_rate_type",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "policy_supplier_account_id",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "policy_supplier_eligibility_version",
                schema: "purchase_ordering",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "policy_total_pen",
                schema: "purchase_ordering",
                table: "purchase_orders");
        }
    }
}
