using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmkDoc.Infrastructure.Persistence;

#nullable disable

namespace SmkDoc.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260917000000_AddDatasetLayer")]
    public partial class AddDatasetLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create datasets table
            migrationBuilder.CreateTable(
                name: "datasets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DataConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SqlQuery = table.Column<string>(type: "text", nullable: false),
                    CacheSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_datasets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_datasets_data_connections_DataConnectionId",
                        column: x => x.DataConnectionId,
                        principalTable: "data_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_datasets_DataConnectionId",
                table: "datasets",
                column: "DataConnectionId");

            // 2. Add new columns to field_mappings
            migrationBuilder.AddColumn<Guid>(
                name: "DatasetId",
                table: "field_mappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultPath",
                table: "field_mappings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            // 3. Add FK for DatasetId
            migrationBuilder.AddForeignKey(
                name: "FK_field_mappings_datasets_DatasetId",
                table: "field_mappings",
                column: "DatasetId",
                principalTable: "datasets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_field_mappings_DatasetId",
                table: "field_mappings",
                column: "DatasetId");

            // 4. Drop old columns
            migrationBuilder.DropColumn(name: "DataConnectionId", table: "field_mappings");
            migrationBuilder.DropColumn(name: "SqlQuery",          table: "field_mappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore old columns
            migrationBuilder.AddColumn<Guid>(
                name: "DataConnectionId",
                table: "field_mappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SqlQuery",
                table: "field_mappings",
                type: "text",
                nullable: true);

            // Remove new FK + columns
            migrationBuilder.DropForeignKey(name: "FK_field_mappings_datasets_DatasetId", table: "field_mappings");
            migrationBuilder.DropIndex(name: "IX_field_mappings_DatasetId", table: "field_mappings");
            migrationBuilder.DropColumn(name: "DatasetId",   table: "field_mappings");
            migrationBuilder.DropColumn(name: "ResultPath",  table: "field_mappings");

            // Drop datasets table
            migrationBuilder.DropTable(name: "datasets");
        }
    }
}
