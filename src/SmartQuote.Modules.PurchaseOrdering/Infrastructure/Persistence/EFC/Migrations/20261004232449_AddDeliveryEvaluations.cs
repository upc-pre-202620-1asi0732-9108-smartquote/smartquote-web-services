using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.PurchaseOrdering.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "delivered_at",
                schema: "purchase_ordering",
                table: "purchase_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "delivery_evaluations",
                schema: "purchase_ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_tax_identifier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    on_time_score = table.Column<int>(type: "integer", nullable: false),
                    quality_score = table.Column<int>(type: "integer", nullable: false),
                    observations = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    evaluated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_evaluations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_evaluations_purchase_order_id",
                schema: "purchase_ordering",
                table: "delivery_evaluations",
                column: "purchase_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_evaluations_supplier_tax_identifier",
                schema: "purchase_ordering",
                table: "delivery_evaluations",
                column: "supplier_tax_identifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_evaluations",
                schema: "purchase_ordering");

            migrationBuilder.DropColumn(
                name: "delivered_at",
                schema: "purchase_ordering",
                table: "purchase_orders");
        }
    }
}
