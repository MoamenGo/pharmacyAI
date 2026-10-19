using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsAndCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardReference",
                table: "Sales",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Sales",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "Sales",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Sales",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                // Every sale before this migration was a cash sale.
                defaultValue: "Cash");

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "SaleLines",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Balance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_BusinessDate",
                table: "CustomerPayments",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CustomerId_PaidAtUtc",
                table: "CustomerPayments",
                columns: new[] { "CustomerId", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                table: "Customers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone",
                unique: true,
                filter: "\"Phone\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Customers_CustomerId",
                table: "Sales",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Customers_CustomerId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "CustomerPayments");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CardReference",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "SaleLines");
        }
    }
}
