using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Document = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PreviousExternalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FirstExternalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReplacedExternalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Kind_Sequence",
                table: "OutboxMessages",
                columns: new[] { "Kind", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Kind_SubjectId",
                table: "OutboxMessages",
                columns: new[] { "Kind", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAtUtc",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            // Queue every sale made before the outbox existed, so none is left unreported.
            // Ids use EF's TEXT Guid format (upper-case, hyphenated).
            migrationBuilder.Sql("""
                INSERT INTO "OutboxMessages" ("Id", "Kind", "SubjectId", "Sequence", "CreatedAtUtc", "Status", "Attempts", "NextAttemptAtUtc")
                SELECT substr(h, 1, 8) || '-' || substr(h, 9, 4) || '-' || substr(h, 13, 4) || '-' || substr(h, 17, 4) || '-' || substr(h, 21, 12),
                       'eta.receipt', "Id", "Number", "SoldAtUtc", 'Pending', 0, "SoldAtUtc"
                FROM (SELECT hex(randomblob(16)) AS h, "Id", "Number", "SoldAtUtc" FROM "Sales");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");
        }
    }
}
