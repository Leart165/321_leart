using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analytics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "owner_monthly",
                columns: table => new
                {
                    owner_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    income = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    expenses = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    transactions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owner_monthly", x => new { x.owner_id, x.year, x.month, x.currency });
                });

            migrationBuilder.CreateTable(
                name: "processed_transactions",
                columns: table => new
                {
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_transactions", x => x.transaction_id);
                });

            migrationBuilder.CreateTable(
                name: "system_daily",
                columns: table => new
                {
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    volume = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    deposits = table.Column<int>(type: "integer", nullable: false),
                    withdrawals = table.Column<int>(type: "integer", nullable: false),
                    transfers = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_daily", x => new { x.day, x.currency });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "owner_monthly");

            migrationBuilder.DropTable(
                name: "processed_transactions");

            migrationBuilder.DropTable(
                name: "system_daily");
        }
    }
}
