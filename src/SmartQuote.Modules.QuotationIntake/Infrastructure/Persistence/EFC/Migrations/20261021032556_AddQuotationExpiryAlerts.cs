using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationExpiryAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quotation_expiry_alerts",
                schema: "quotation_intake",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    lead_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reassigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotation_expiry_alerts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quotation_expiry_alerts_quotation_id_valid_until_lead_days",
                schema: "quotation_intake",
                table: "quotation_expiry_alerts",
                columns: new[] { "quotation_id", "valid_until", "lead_days" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotation_expiry_alerts_recipient_id_read_at",
                schema: "quotation_intake",
                table: "quotation_expiry_alerts",
                columns: new[] { "recipient_id", "read_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quotation_expiry_alerts",
                schema: "quotation_intake");
        }
    }
}
