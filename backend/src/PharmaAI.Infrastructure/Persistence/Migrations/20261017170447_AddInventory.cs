using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drugs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PackUnitName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SubUnitName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SubUnitsPerPack = table.Column<int>(type: "INTEGER", nullable: false),
                    PublicPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drugs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DrugId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    QuantityInSubUnits = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBatches", x => x.Id);
                    table.CheckConstraint("CK_StockBatches_Quantity_NonNegative", "\"QuantityInSubUnits\" >= 0");
                    table.ForeignKey(
                        name: "FK_StockBatches_Drugs_DrugId",
                        column: x => x.DrugId,
                        principalTable: "Drugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_Barcode",
                table: "Drugs",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_NameAr",
                table: "Drugs",
                column: "NameAr");

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_DrugId_BatchNumber_ExpiryDate",
                table: "StockBatches",
                columns: new[] { "DrugId", "BatchNumber", "ExpiryDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_DrugId_ExpiryDate",
                table: "StockBatches",
                columns: new[] { "DrugId", "ExpiryDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockBatches");

            migrationBuilder.DropTable(
                name: "Drugs");
        }
    }
}
