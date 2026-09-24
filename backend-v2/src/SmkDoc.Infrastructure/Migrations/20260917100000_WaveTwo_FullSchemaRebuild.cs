using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmkDoc.Infrastructure.Persistence;

#nullable disable

namespace SmkDoc.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260917100000_WaveTwo_FullSchemaRebuild")]
    public partial class WaveTwo_FullSchemaRebuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ================================================================
            // 1. Apply AddDatasetLayer changes first (if not already applied)
            //    These guards use IF NOT EXISTS / IF EXISTS so they are safe
            //    whether the previous migration ran or not.
            // ================================================================

            // 1a. Create datasets table (AddDatasetLayer)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS datasets (
                    ""Id""               uuid NOT NULL,
                    ""Name""             character varying(200) NOT NULL,
                    ""Description""      character varying(500),
                    ""DataConnectionId"" uuid NOT NULL,
                    ""SqlQuery""         text NOT NULL,
                    ""CacheSeconds""     integer NOT NULL DEFAULT 0,
                    ""CreatedAt""        timestamp with time zone NOT NULL DEFAULT NOW(),
                    ""UpdatedAt""        timestamp with time zone,
                    CONSTRAINT ""PK_datasets"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_datasets_data_connections_DataConnectionId""
                        FOREIGN KEY (""DataConnectionId"")
                        REFERENCES data_connections (""Id"") ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS ""IX_datasets_DataConnectionId""
                    ON datasets (""DataConnectionId"");
            ");

            // 1b. field_mappings: add DatasetId + ResultPath (AddDatasetLayer)
            migrationBuilder.Sql(@"
                ALTER TABLE field_mappings
                    ADD COLUMN IF NOT EXISTS ""DatasetId"" uuid REFERENCES datasets(""Id"") ON DELETE SET NULL,
                    ADD COLUMN IF NOT EXISTS ""ResultPath"" character varying(300);
                CREATE INDEX IF NOT EXISTS ""IX_field_mappings_DatasetId""
                    ON field_mappings(""DatasetId"");
            ");

            // 1c. Remove old direct-SQL columns from field_mappings (AddDatasetLayer)
            migrationBuilder.Sql(@"
                ALTER TABLE field_mappings
                    DROP COLUMN IF EXISTS ""DataConnectionId"",
                    DROP COLUMN IF EXISTS ""SqlQuery"";
            ");

            // ================================================================
            // 2. api_keys: add ExpiresAt
            // ================================================================
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "api_keys",
                type: "timestamp with time zone",
                nullable: true);

            // ================================================================
            // 3. field_mappings: drop DatasetId FK (moved to alias string),
            //    add DatasetAlias + MathExpression
            // ================================================================
            migrationBuilder.Sql(@"
                ALTER TABLE field_mappings
                    DROP CONSTRAINT IF EXISTS ""FK_field_mappings_datasets_DatasetId"";
            ");
            migrationBuilder.DropIndex(
                name: "IX_field_mappings_DatasetId",
                table: "field_mappings");
            migrationBuilder.Sql(@"
                ALTER TABLE field_mappings
                    DROP COLUMN IF EXISTS ""DatasetId"";
            ");
            migrationBuilder.AddColumn<string>(
                name: "DatasetAlias",
                table: "field_mappings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "MathExpression",
                table: "field_mappings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // ================================================================
            // 4. template_versions: rename ChangeNote→CommitMessage, add columns
            // ================================================================
            migrationBuilder.RenameColumn(
                name: "ChangeNote",
                table: "template_versions",
                newName: "CommitMessage");
            migrationBuilder.AlterColumn<string>(
                name: "CommitMessage",
                table: "template_versions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "template_versions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.AddColumn<string>(
                name: "FileFormat",
                table: "template_versions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "DataSchema",
                table: "template_versions",
                type: "jsonb",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "SamplePayload",
                table: "template_versions",
                type: "jsonb",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "MappingsSnapshot",
                table: "template_versions",
                type: "jsonb",
                nullable: true);

            // ================================================================
            // 5. templates: drop Version + StorageKey, add CurrentVersionId FK
            // ================================================================
            migrationBuilder.DropColumn(
                name: "Version",
                table: "templates");
            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "templates");
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentVersionId",
                table: "templates",
                type: "uuid",
                nullable: true);
            migrationBuilder.CreateIndex(
                name: "IX_templates_CurrentVersionId",
                table: "templates",
                column: "CurrentVersionId");
            migrationBuilder.AddForeignKey(
                name: "FK_templates_template_versions_CurrentVersionId",
                table: "templates",
                column: "CurrentVersionId",
                principalTable: "template_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ================================================================
            // 6. generation_logs: drop TemplateVer, add TemplateVersionId + new fields
            // ================================================================
            migrationBuilder.DropColumn(
                name: "TemplateVer",
                table: "generation_logs");
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateVersionId",
                table: "generation_logs",
                type: "uuid",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "TriggerSource",
                table: "generation_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "generation_logs",
                type: "bigint",
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "PageCount",
                table: "generation_logs",
                type: "integer",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "PayloadHashSha256",
                table: "generation_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
            migrationBuilder.CreateIndex(
                name: "IX_generation_logs_TemplateVersionId",
                table: "generation_logs",
                column: "TemplateVersionId");
            migrationBuilder.AddForeignKey(
                name: "FK_generation_logs_template_versions_TemplateVersionId",
                table: "generation_logs",
                column: "TemplateVersionId",
                principalTable: "template_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ================================================================
            // 7. Create documents table (anchor entity for document versioning)
            // ================================================================
            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documents_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });
            migrationBuilder.CreateIndex(
                name: "IX_documents_DocumentRef",
                table: "documents",
                column: "DocumentRef",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_documents_TemplateId",
                table: "documents",
                column: "TemplateId");

            // ================================================================
            // 8. Restructure document_versions: drop old columns, add new FKs
            // ================================================================
            migrationBuilder.DropIndex(
                name: "IX_document_versions_DocumentRef_Version",
                table: "document_versions");
            migrationBuilder.DropIndex(
                name: "IX_document_versions_TemplateId",
                table: "document_versions");
            migrationBuilder.DropForeignKey(
                name: "FK_document_versions_templates_TemplateId",
                table: "document_versions");
            migrationBuilder.DropColumn(name: "DocumentRef",  table: "document_versions");
            migrationBuilder.DropColumn(name: "InputData",    table: "document_versions");
            migrationBuilder.DropColumn(name: "OutputFormat", table: "document_versions");
            migrationBuilder.DropColumn(name: "OutputKey",    table: "document_versions");
            migrationBuilder.DropColumn(name: "TemplateId",   table: "document_versions");
            migrationBuilder.DropColumn(name: "TemplateVer",  table: "document_versions");

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "document_versions",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateVersionId",
                table: "document_versions",
                type: "uuid",
                nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "GenerationLogId",
                table: "document_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_DocumentId_Version",
                table: "document_versions",
                columns: new[] { "DocumentId", "Version" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_document_versions_TemplateVersionId",
                table: "document_versions",
                column: "TemplateVersionId");
            migrationBuilder.CreateIndex(
                name: "IX_document_versions_GenerationLogId",
                table: "document_versions",
                column: "GenerationLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_document_versions_documents_DocumentId",
                table: "document_versions",
                column: "DocumentId",
                principalTable: "documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_document_versions_template_versions_TemplateVersionId",
                table: "document_versions",
                column: "TemplateVersionId",
                principalTable: "template_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(
                name: "FK_document_versions_generation_logs_GenerationLogId",
                table: "document_versions",
                column: "GenerationLogId",
                principalTable: "generation_logs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ================================================================
            // 9. Create template_datasets table
            // ================================================================
            migrationBuilder.CreateTable(
                name: "template_datasets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_template_datasets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_template_datasets_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_template_datasets_datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(
                name: "IX_template_datasets_TemplateId_Alias",
                table: "template_datasets",
                columns: new[] { "TemplateId", "Alias" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_template_datasets_DatasetId",
                table: "template_datasets",
                column: "DatasetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 9. Drop template_datasets
            migrationBuilder.DropTable(name: "template_datasets");

            // 8. Revert document_versions
            migrationBuilder.DropForeignKey(name: "FK_document_versions_documents_DocumentId", table: "document_versions");
            migrationBuilder.DropForeignKey(name: "FK_document_versions_template_versions_TemplateVersionId", table: "document_versions");
            migrationBuilder.DropForeignKey(name: "FK_document_versions_generation_logs_GenerationLogId", table: "document_versions");
            migrationBuilder.DropIndex(name: "IX_document_versions_DocumentId_Version", table: "document_versions");
            migrationBuilder.DropIndex(name: "IX_document_versions_TemplateVersionId", table: "document_versions");
            migrationBuilder.DropIndex(name: "IX_document_versions_GenerationLogId", table: "document_versions");
            migrationBuilder.DropColumn(name: "DocumentId",      table: "document_versions");
            migrationBuilder.DropColumn(name: "TemplateVersionId", table: "document_versions");
            migrationBuilder.DropColumn(name: "GenerationLogId", table: "document_versions");

            migrationBuilder.AddColumn<string>(name: "DocumentRef",  table: "document_versions", type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "InputData",    table: "document_versions", type: "jsonb", nullable: true);
            migrationBuilder.AddColumn<string>(name: "OutputFormat", table: "document_versions", type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "OutputKey",    table: "document_versions", type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<Guid?>(name: "TemplateId",    table: "document_versions", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<int?>(name: "TemplateVer",    table: "document_versions", type: "integer", nullable: true);

            migrationBuilder.CreateIndex(name: "IX_document_versions_DocumentRef_Version", table: "document_versions", columns: new[] { "DocumentRef", "Version" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_document_versions_TemplateId", table: "document_versions", column: "TemplateId");
            migrationBuilder.AddForeignKey(name: "FK_document_versions_templates_TemplateId", table: "document_versions", column: "TemplateId", principalTable: "templates", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // 7. Drop documents
            migrationBuilder.DropTable(name: "documents");

            // 6. Revert generation_logs
            migrationBuilder.DropForeignKey(name: "FK_generation_logs_template_versions_TemplateVersionId", table: "generation_logs");
            migrationBuilder.DropIndex(name: "IX_generation_logs_TemplateVersionId", table: "generation_logs");
            migrationBuilder.DropColumn(name: "TemplateVersionId", table: "generation_logs");
            migrationBuilder.DropColumn(name: "TriggerSource",     table: "generation_logs");
            migrationBuilder.DropColumn(name: "FileSizeBytes",     table: "generation_logs");
            migrationBuilder.DropColumn(name: "PageCount",         table: "generation_logs");
            migrationBuilder.DropColumn(name: "PayloadHashSha256", table: "generation_logs");
            migrationBuilder.AddColumn<int?>(name: "TemplateVer",  table: "generation_logs", type: "integer", nullable: true);

            // 5. Revert templates
            migrationBuilder.DropForeignKey(name: "FK_templates_template_versions_CurrentVersionId", table: "templates");
            migrationBuilder.DropIndex(name: "IX_templates_CurrentVersionId", table: "templates");
            migrationBuilder.DropColumn(name: "CurrentVersionId", table: "templates");
            migrationBuilder.AddColumn<int>(name: "Version",       table: "templates", type: "integer", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<string>(name: "StorageKey", table: "templates", type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: "");

            // 4. Revert template_versions
            migrationBuilder.DropColumn(name: "Status",           table: "template_versions");
            migrationBuilder.DropColumn(name: "FileFormat",       table: "template_versions");
            migrationBuilder.DropColumn(name: "DataSchema",       table: "template_versions");
            migrationBuilder.DropColumn(name: "SamplePayload",    table: "template_versions");
            migrationBuilder.DropColumn(name: "MappingsSnapshot", table: "template_versions");
            migrationBuilder.RenameColumn(name: "CommitMessage",  table: "template_versions", newName: "ChangeNote");

            // 3. Revert field_mappings
            migrationBuilder.DropColumn(name: "DatasetAlias",    table: "field_mappings");
            migrationBuilder.DropColumn(name: "MathExpression",  table: "field_mappings");
            migrationBuilder.AddColumn<Guid?>(name: "DatasetId", table: "field_mappings", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ResultPath", table: "field_mappings", type: "character varying(300)", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_field_mappings_DatasetId", table: "field_mappings", column: "DatasetId");
            migrationBuilder.AddForeignKey(name: "FK_field_mappings_datasets_DatasetId", table: "field_mappings", column: "DatasetId", principalTable: "datasets", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // 2. Revert api_keys
            migrationBuilder.DropColumn(name: "ExpiresAt", table: "api_keys");

            // 1. Revert AddDatasetLayer (restore old field_mappings columns, drop datasets)
            migrationBuilder.DropForeignKey(name: "FK_field_mappings_datasets_DatasetId", table: "field_mappings");
            migrationBuilder.DropIndex(name: "IX_field_mappings_DatasetId", table: "field_mappings");
            migrationBuilder.DropColumn(name: "DatasetId",   table: "field_mappings");
            migrationBuilder.DropColumn(name: "ResultPath",  table: "field_mappings");
            migrationBuilder.AddColumn<Guid?>(name: "DataConnectionId", table: "field_mappings", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>(name: "SqlQuery", table: "field_mappings", type: "text", nullable: true);
            migrationBuilder.DropTable(name: "datasets");
        }
    }
}
