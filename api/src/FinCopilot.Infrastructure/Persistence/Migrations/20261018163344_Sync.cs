using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_card_statements_card_id_statement_date",
                table: "card_statements");

            migrationBuilder.AddColumn<string>(
                name: "direction",
                table: "card_transactions",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_card_statements_card_id_statement_date",
                table: "card_statements",
                columns: new[] { "card_id", "statement_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_card_statements_card_id_statement_date",
                table: "card_statements");

            migrationBuilder.DropColumn(
                name: "direction",
                table: "card_transactions");

            migrationBuilder.CreateIndex(
                name: "ix_card_statements_card_id_statement_date",
                table: "card_statements",
                columns: new[] { "card_id", "statement_date" });
        }
    }
}
