using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class InitialQuotationIntakeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "quotation_intake");

            migrationBuilder.CreateTable(
                name: "poultry_quotes",
                schema: "quotation_intake",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_reference_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    supplier_business_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_tax_identifier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_sha256 = table.Column<string>(type: "char(64)", nullable: false),
                    duplicate_key = table.Column<string>(type: "character varying(110)", maxLength: 110, nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    currency = table.Column<string>(type: "char(3)", nullable: true),
                    delivery_lead_time_days = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_poultry_quotes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "extracted_fields",
                schema: "quotation_intake",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_path = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    original_value = table.Column<string>(type: "text", nullable: true),
                    current_value = table.Column<string>(type: "text", nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    source_page = table.Column<int>(type: "integer", nullable: false),
                    source_text_reference = table.Column<string>(type: "text", nullable: false),
                    resolution_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    poultry_quote_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_extracted_fields", x => x.id);
                    table.ForeignKey(
                        name: "fk_extracted_fields_poultry_quotes_poultry_quote_id",
                        column: x => x.poultry_quote_id,
                        principalSchema: "quotation_intake",
                        principalTable: "poultry_quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quotation_lines",
                schema: "quotation_intake",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    unit_of_measure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    requested_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    poultry_quote_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotation_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_quotation_lines_poultry_quotes_poultry_quote_id",
                        column: x => x.poultry_quote_id,
                        principalSchema: "quotation_intake",
                        principalTable: "poultry_quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "field_corrections",
                schema: "quotation_intake",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_value = table.Column<string>(type: "text", nullable: false),
                    corrected_value = table.Column<string>(type: "text", nullable: false),
                    corrected_by = table.Column<Guid>(type: "uuid", nullable: false),
                    corrected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    extracted_field_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_corrections", x => x.id);
                    table.ForeignKey(
                        name: "fk_field_corrections_extracted_fields_extracted_field_id",
                        column: x => x.extracted_field_id,
                        principalSchema: "quotation_intake",
                        principalTable: "extracted_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quoted_specifications",
                schema: "quotation_intake",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    value = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quotation_line_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quoted_specifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_quoted_specifications_quotation_lines_quotation_line_id",
                        column: x => x.quotation_line_id,
                        principalSchema: "quotation_intake",
                        principalTable: "quotation_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_extracted_fields_poultry_quote_id",
                schema: "quotation_intake",
                table: "extracted_fields",
                column: "poultry_quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_field_corrections_extracted_field_id",
                schema: "quotation_intake",
                table: "field_corrections",
                column: "extracted_field_id");

            migrationBuilder.CreateIndex(
                name: "ix_poultry_quotes_duplicate_key",
                schema: "quotation_intake",
                table: "poultry_quotes",
                column: "duplicate_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotation_lines_poultry_quote_id",
                schema: "quotation_intake",
                table: "quotation_lines",
                column: "poultry_quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_quoted_specifications_quotation_line_id",
                schema: "quotation_intake",
                table: "quoted_specifications",
                column: "quotation_line_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "field_corrections",
                schema: "quotation_intake");

            migrationBuilder.DropTable(
                name: "quoted_specifications",
                schema: "quotation_intake");

            migrationBuilder.DropTable(
                name: "extracted_fields",
                schema: "quotation_intake");

            migrationBuilder.DropTable(
                name: "quotation_lines",
                schema: "quotation_intake");

            migrationBuilder.DropTable(
                name: "poultry_quotes",
                schema: "quotation_intake");
        }
    }
}
