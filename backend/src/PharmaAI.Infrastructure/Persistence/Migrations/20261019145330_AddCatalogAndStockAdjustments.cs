using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAndStockAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveIngredient",
                table: "Drugs",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "Drugs",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReorderLevelInPacks",
                table: "Drugs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CatalogDrugs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ActiveIngredient = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    DrugClass = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    Route = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogDrugs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DrugId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StockBatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    QuantityInSubUnits = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustments", x => x.Id);
                    table.CheckConstraint("CK_StockAdjustments_Quantity_Positive", "\"QuantityInSubUnits\" > 0");
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Drugs_DrugId",
                        column: x => x.DrugId,
                        principalTable: "Drugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_StockBatches_StockBatchId",
                        column: x => x.StockBatchId,
                        principalTable: "StockBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogDrugs_ActiveIngredient",
                table: "CatalogDrugs",
                column: "ActiveIngredient");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogDrugs_NameEn",
                table: "CatalogDrugs",
                column: "NameEn");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_CreatedAtUtc",
                table: "StockAdjustments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_DrugId",
                table: "StockAdjustments",
                column: "DrugId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_StockBatchId",
                table: "StockAdjustments",
                column: "StockBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogDrugs");

            migrationBuilder.DropTable(
                name: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "ActiveIngredient",
                table: "Drugs");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "Drugs");

            migrationBuilder.DropColumn(
                name: "ReorderLevelInPacks",
                table: "Drugs");
        }
    }
}
