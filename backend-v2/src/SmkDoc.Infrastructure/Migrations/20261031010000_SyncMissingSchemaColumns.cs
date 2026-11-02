using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmkDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncMissingSchemaColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. api_keys
            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "api_keys",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ReadWrite");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "api_keys",
                type: "timestamp with time zone",
                nullable: true);

            // 2. documents
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            // 3. document_versions
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "document_versions",
                type: "timestamp with time zone",
                nullable: true);

            // 4. field_mappings
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "field_mappings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "field_mappings",
                type: "timestamp with time zone",
                nullable: true);

            // 5. generation_logs
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "generation_logs",
                type: "timestamp with time zone",
                nullable: true);

            // 6. template_datasets
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "template_datasets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "template_datasets",
                type: "timestamp with time zone",
                nullable: true);

            // 7. template_versions
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "template_versions",
                type: "timestamp with time zone",
                nullable: true);

            // 8. refresh_tokens table
            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId_ExpiresAt",
                table: "refresh_tokens",
                columns: new[] { "UserId", "ExpiresAt" },
                filter: "\"RevokedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "refresh_tokens");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "template_versions");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "template_datasets");
            migrationBuilder.DropColumn(name: "CreatedAt", table: "template_datasets");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "generation_logs");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "field_mappings");
            migrationBuilder.DropColumn(name: "CreatedAt", table: "field_mappings");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "document_versions");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "documents");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "api_keys");
            migrationBuilder.DropColumn(name: "Scope", table: "api_keys");
        }
    }
}

