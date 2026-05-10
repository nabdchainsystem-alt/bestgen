using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bestgen.Migrations
{
    /// <inheritdoc />
    public partial class AddImportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Entity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnMappingJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ErrorSummary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_TenantId",
                table: "ImportJobs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportJobs");
        }
    }
}
