using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmkDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4OutboxIdempotencyTenantManagementAndJobQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedEgressCidrs",
                table: "projects",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "projects",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "companies",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "user_project_roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "user_project_roles",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "templates",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "documents",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "datasets",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "data_connections",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AlterColumn<string>(
                name: "KeyHash",
                table: "api_keys",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "KeyPrefix",
                table: "api_keys",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "smk_live");

            migrationBuilder.AddColumn<string>(
                name: "KeyId",
                table: "api_keys",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeySuffix",
                table: "api_keys",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SecretHashBytes",
                table: "api_keys",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "api_keys",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_KeyId",
                table: "api_keys",
                column: "KeyId",
                unique: true,
                filter: "\"KeyId\" IS NOT NULL");

            migrationBuilder.CreateTable(
                name: "project_quotas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxRequestsPerMinute = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    MaxConcurrentJobs = table.Column<int>(type: "integer", nullable: false, defaultValue: 4),
                    MaxBatchItems = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    MaxStorageBytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 5368709120L),
                    MaxActiveApiKeys = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_quotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_quotas_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_quotas_ProjectId",
                table: "project_quotas",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateTable(
                name: "fonts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Weight = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Style = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsSystemFont = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fonts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fonts_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fonts_ProjectId_FamilyName_Weight_Style",
                table: "fonts",
                columns: new[] { "ProjectId", "FamilyName", "Weight", "Style" });

            migrationBuilder.CreateTable(
                name: "break_glass_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuperAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_break_glass_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_break_glass_grants_projects_TargetProjectId",
                        column: x => x.TargetProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_break_glass_grants_users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_break_glass_grants_users_SuperAdminUserId",
                        column: x => x.SuperAdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_break_glass_grants_GrantedByUserId",
                table: "break_glass_grants",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_break_glass_grants_SuperAdminUserId_TargetProjectId_ExpiresAt",
                table: "break_glass_grants",
                columns: new[] { "SuperAdminUserId", "TargetProjectId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_break_glass_grants_TargetProjectId",
                table: "break_glass_grants",
                column: "TargetProjectId");

            migrationBuilder.CreateTable(
                name: "document_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CallerIdentity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OutputFormat = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WebhookUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CancelRequested = table.Column<bool>(type: "boolean", nullable: false),
                    ResultGenerationLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_jobs_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_jobs_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_jobs_ProjectId_CreatedAt",
                table: "document_jobs",
                columns: new[] { "ProjectId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_document_jobs_ProjectId_IdempotencyKey",
                table: "document_jobs",
                columns: new[] { "ProjectId", "IdempotencyKey" },
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_document_jobs_Status_AvailableAt_CreatedAt",
                table: "document_jobs",
                columns: new[] { "Status", "AvailableAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_jobs_TemplateId",
                table: "document_jobs",
                column: "TemplateId");

            migrationBuilder.CreateTable(
                name: "document_job_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemIndex = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    DocumentRef = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GenerationLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutputStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_job_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_job_items_document_jobs_DocumentJobId",
                        column: x => x.DocumentJobId,
                        principalTable: "document_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_job_items_DocumentJobId_ItemIndex",
                table: "document_job_items",
                columns: new[] { "DocumentJobId", "ItemIndex" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_unprocessed",
                table: "outbox_messages",
                columns: new[] { "AvailableAt", "OccurredAt" },
                filter: "\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL");

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantScopeId = table.Column<Guid>(type: "uuid", nullable: false, computedColumnSql: "COALESCE(\"ProjectId\", '00000000-0000-0000-0000-000000000000'::uuid)", stored: true),
                    CredentialId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Scope = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResponseHeadersJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResultResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_keys_ExpiresAt",
                table: "idempotency_keys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "UQ_idempotency_keys_scope",
                table: "idempotency_keys",
                columns: new[] { "TenantScopeId", "CredentialId", "Scope", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "idempotency_keys");
            migrationBuilder.DropTable(name: "outbox_messages");
            migrationBuilder.DropTable(name: "document_job_items");
            migrationBuilder.DropTable(name: "document_jobs");
            migrationBuilder.DropTable(name: "break_glass_grants");
            migrationBuilder.DropTable(name: "fonts");
            migrationBuilder.DropTable(name: "project_quotas");

            migrationBuilder.DropIndex(name: "IX_api_keys_KeyId", table: "api_keys");
            migrationBuilder.DropColumn(name: "KeyPrefix", table: "api_keys");
            migrationBuilder.DropColumn(name: "KeyId", table: "api_keys");
            migrationBuilder.DropColumn(name: "KeySuffix", table: "api_keys");
            migrationBuilder.DropColumn(name: "SecretHashBytes", table: "api_keys");
            migrationBuilder.DropColumn(name: "Version", table: "api_keys");

            migrationBuilder.Sql("UPDATE public.api_keys SET \"KeyHash\" = '' WHERE \"KeyHash\" IS NULL;");
            migrationBuilder.AlterColumn<string>(
                name: "KeyHash",
                table: "api_keys",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.DropColumn(name: "Version", table: "data_connections");
            migrationBuilder.DropColumn(name: "Version", table: "datasets");
            migrationBuilder.DropColumn(name: "Version", table: "documents");
            migrationBuilder.DropColumn(name: "Version", table: "templates");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "user_project_roles");
            migrationBuilder.DropColumn(name: "Version", table: "user_project_roles");
            migrationBuilder.DropColumn(name: "Version", table: "users");
            migrationBuilder.DropColumn(name: "Version", table: "companies");
            migrationBuilder.DropColumn(name: "AllowedEgressCidrs", table: "projects");
            migrationBuilder.DropColumn(name: "Version", table: "projects");
        }
    }
}
