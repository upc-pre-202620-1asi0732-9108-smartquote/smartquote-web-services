using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.PurchaseOrdering.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class UniqueOrderPerRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_source_purchase_request_id",
                schema: "purchase_ordering",
                table: "purchase_orders",
                column: "source_purchase_request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_purchase_orders_source_purchase_request_id",
                schema: "purchase_ordering",
                table: "purchase_orders");
        }
    }
}
