# DB_SCHEMA.md — PostgreSQL Relational Schema & Tables

> **[PURPOSE_SCOPE_SSOT]:** Authoritative Single Source of Truth (SSoT) for PostgreSQL relational schema definition, UUIDv7 PK types, foreign keys, index strategy, Domain Value Object mappings, and JSONB contracts for `smk-doc-server`.
> **[CORE_MISSION]:** Eliminate persistence-layer LLM hallucinations, enforce enterprise PostgreSQL 16+ best practices, guarantee 100% Clean Architecture domain decoupling, and maintain high-throughput indexing hygiene.
> **[RELATED_DOCS]:** [ARCHITECTURE.md](ARCHITECTURE.md) (Persistence layer & system topology), [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) (Domain entities), [PATTERNS.md](PATTERNS.md) (Clean Architecture archetypes), [CODING_CONVENTIONS.md](CODING_CONVENTIONS.md) (Naming standards), [ANTI-PATTERNS.md](ANTI-PATTERNS.md) (Persistence pitfalls).

---

<db_cognitive_framing>
## 🧠 AI Cognitive Framing & Attention Routing

### Operational Modes & Attention Budget
When processing this database specification, the LLM MUST determine its active operational mode:
1. **`[MODE: SCHEMA_DESIGN_CODEGEN]`**: Focus attention strictly on `<db_syntax_substitution_map>`, `<db_design_invariants>`, `<authoritative_table_catalog>`, `<domain_persistence_matrix>`, `<indexing_and_concurrency_strategy>`, and `<authoritative_ddl_spec>`.
2. **`[MODE: DB_MIGRATION_AUDIT]`**: Focus on `<db_design_invariants>`, `<authoritative_ddl_spec>`, and `<db_maintenance_and_migration_rules>` to verify referential integrity, explicit FK indexes, and zero-loss migrations.

### Persistence Severity Tiers (Cognitive Constraint Hierarchy)
To prevent constraint collapse during schema design and migration authoring, resolve priority collisions using this hierarchy:
* **`[TIER-1-FATAL]` (Data & Process Integrity):** Application-generated UUIDv7 PKs, mandatory indexes on ALL Foreign Keys, non-destructive referential actions on audit logs (`RESTRICT` / `SET NULL`), Clean Architecture domain isolation (`.HasConversion()`).
* **`[TIER-2-INTEGRITY]` (Schema & Concurrency):** Correct soft-delete convention (`is_active` boolean), PostgreSQL `xmin` optimistic concurrency tokens, strict unique constraints (`slug`, `document_ref`).
* **`[TIER-3-INDEX-OPT]` (Storage & RAM Optimization):** Partial/filtered indexes (`WHERE revoked_at IS NULL`), chronological log sorting indexes (`created_at DESC`), JSONB Draft-07 schema validation.
</db_cognitive_framing>

---

<db_syntax_substitution_map>
## ⚡ High-Salience Database Syntax Substitution Map (Pre-Training Override)

> **[COGNITIVE_PURPOSE]:** Direct hash-lookup table designed to immediately override pre-training priors across all Transformer attention heads. Whenever the model contemplates generating a pattern from the left column, it MUST instantly substitute it with the canonical pattern from the right column.

| Database / Persistence Context | Defective Prior (❌ Banned) | Canonical Standard (✅ Mandatory Target) | Governing Invariant |
|---|---|---|---|
| **Primary Key Generation** | `id UUID DEFAULT gen_random_uuid()`, `SERIAL` | `id UUID PRIMARY KEY` (Populated via C# `Guid.CreateVersion7()`) | Monotonic B-Tree append locality |
| **Foreign Key Indexing** | Omitting index on FK column | Explicit `CREATE INDEX idx_{table}_{fk}` on every FK column | Zero sequential scan on JOIN/Cascades |
| **Soft Delete Pattern** | `deleted_at TIMESTAMPTZ` | `is_active BOOLEAN NOT NULL DEFAULT true` | EF Core Global Query Filter parity |
| **Audit Log Referential Action** | `template_id UUID REFERENCES ... ON DELETE CASCADE` | `ON DELETE SET NULL` or `RESTRICT` | Non-destructive legal audit trail |
| **Token Session Indexing** | Indexing entire `refresh_tokens(user_id)` | Partial index `WHERE revoked_at IS NULL` | RAM-efficient filtered indexing |
| **Smart Enum Persistence** | Native C# integer enum or raw int | String representation via `.FromDisplayName<T>()` (Except VersionStatus=int) | Human-readable & schema-resilient storage |
| **Value Object Persistence** | Exposing primitive strings on Domain POCO | `.HasConversion(vo => vo.Value, v => VO.Create(v))` | Clean Architecture POCO isolation |
| **Concurrency Defense** | Ignoring concurrency or using client locks | `builder.UseXminAsConcurrencyToken()` in EF Core | PostgreSQL system-level optimistic lock |
| **String Constraints** | Unbounded `TEXT` for all string properties | Bounded `VARCHAR(N)` matching domain invariants | Storage efficiency & schema documentation |
</db_syntax_substitution_map>

---

<db_design_invariants>
## 1. 🏛️ The 6 Enterprise Database Design Pillars

All database tables, migrations, and EF Core configurations across `smk-doc-server` MUST strictly conform to these 6 foundational design pillars:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                   ENTERPRISE POSTGRESQL DESIGN PILLARS (SMK-DOC-SERVER)                │
├────────────────────────┬────────────────────────┬──────────────────────────────────────┤
│ 1. Identity & Locality │ UUIDv7 App-Generated   │ Monotonic B-Tree append locality     │
├────────────────────────┼────────────────────────┼──────────────────────────────────────┤
│ 2. Relationship Safety │ FK Indexing & RESTRICT │ Index all FKs; Zero cascade on logs  │
├────────────────────────┼────────────────────────┼──────────────────────────────────────┤
│ 3. State & Soft Delete │ is_active + Filters    │ Clean boolean query filter mapping   │
├────────────────────────┼────────────────────────┼──────────────────────────────────────┤
│ 4. Partial Indexing    │ Filtered Indexes       │ WHERE revoked_at IS NULL (Small RAM) │
├────────────────────────┼────────────────────────┼──────────────────────────────────────┤
│ 5. Concurrency Defense │ PostgreSQL xmin Token  │ System-level optimistic lock defense │
├────────────────────────┼────────────────────────┼──────────────────────────────────────┤
│ 6. Audit Immutability  │ Snapshots over JOINS   │ JSONB snapshots for legal audit      │
└────────────────────────┴────────────────────────┴──────────────────────────────────────┘
```

1. **Monotonic Primary Key Locality (UUIDv7 Invariant):**
   - Primary keys MUST be generated in the C# Application/Domain layer using `BaseEntity` / `Guid.CreateVersion7()`.
   - Database columns are standard `UUID` without DB-side `gen_random_uuid()` to prevent B-Tree index page-splitting fragmentation. UUIDv7 provides natural time-ordered index locality.
2. **Defensive Referential Integrity (Non-Destructive Audit Trails):**
   - Master entity hierarchies (`companies` ➔ `projects` ➔ `templates`) use `ON DELETE CASCADE` for parent lifecycle cleanup.
   - Financial, operational, and regulatory audit tables (`generation_logs`, `document_versions`, `datasets`) **MUST use `ON DELETE SET NULL` or `RESTRICT`**. Accidental cascading deletion of legal audit records is strictly prohibited.
3. **Mandatory Foreign Key Indexing (Zero Sequential Scans):**
   - PostgreSQL does NOT index foreign keys automatically. Every foreign key column MUST have an explicit `CREATE INDEX` to eliminate full table sequential scans on `JOIN` queries and cascading deletes.
4. **High-Performance Filtered/Partial Indexing:**
   - Use partial indexes with `WHERE` clauses for hot-path queries over filtered subsets of rows (e.g. `WHERE revoked_at IS NULL` on `refresh_tokens`), saving disk space and memory buffers.
5. **Optimistic Concurrency via PostgreSQL `xmin`:**
   - Entities subject to concurrent multi-user editing (e.g., `templates`, `template_versions`) MUST configure PostgreSQL `xmin` system column tokens via `builder.UseXminAsConcurrencyToken()` in EF Core to defend against Lost Updates.
6. **Audit Trail Immutability (Snapshot Pattern):**
   - Generated document records (`generation_logs.input_data`, `template_versions.mappings_snapshot`) MUST store complete structured snapshots at execution time rather than relying on mutable relational joins.
</db_design_invariants>

---

<relational_clusters_and_erd>
## 2. 📊 Enterprise Entity-Relationship Topology (Mermaid ERD)

The database schema is organized into 4 cohesive bounded clusters:

```mermaid
erDiagram
    %% Cluster 1: Multi-Tenancy & Identity
    companies ||--o{ projects : "owns"
    projects ||--o{ user_project_roles : "assigned to"
    users ||--o{ user_project_roles : "has"
    users ||--o{ refresh_tokens : "issues"

    %% Cluster 2: Template Catalog & Mappings
    projects ||--o{ templates : "scopes"
    templates ||--o{ template_versions : "versions"
    templates ||--o{ field_mappings : "defines"
    templates ||--o{ template_datasets : "binds"

    %% Cluster 3: External Datasources & SQL Datasets
    data_connections ||--o{ datasets : "provides connection"
    datasets ||--o{ template_datasets : "referenced by"

    %% Cluster 4: Security, Execution Logs & Legal Audit Trail
    projects ||--o{ api_keys : "issues M2M"
    templates ||--o{ generation_logs : "audits"
    template_versions ||--o{ generation_logs : "snapshot"
    api_keys ||--o{ generation_logs : "triggered by"
    templates ||--o{ documents : "originates"
    documents ||--o{ document_versions : "revises"
    template_versions ||--o{ document_versions : "renders with"
    generation_logs ||--o{ document_versions : "generated by"
```
</relational_clusters_and_erd>

---

<authoritative_table_catalog>
## 3. ⚡ Quick-Lookup: Authoritative 15-Table Catalog (SSoT)

| # | Table Name | Entity Class | Primary Key | Foreign Keys & Targets | Soft Delete | Performance Indexes & Constraints |
|---|---|---|---|---|---|---|
| 1 | `companies` | `Company` | UUIDv7 | None | `is_active` | PK |
| 2 | `projects` | `Project` | UUIDv7 | `company_id` → `companies(id)` [CASCADE] | `is_active` | `(slug)` UNIQUE, `(company_id)` FK |
| 3 | `users` | `User` | UUIDv7 | None | `is_active` | `(email)` UNIQUE |
| 4 | `user_project_roles` | `UserProjectRole` | `(user_id, project_id)` | `user_id` → `users(id)` [CASCADE]<br>`project_id` → `projects(id)` [CASCADE] | None | PK composite, `(project_id)` FK |
| 5 | `refresh_tokens` | `RefreshToken` | UUIDv7 | `user_id` → `users(id)` [CASCADE] | None | `(token_hash)` UNIQUE, `(user_id, expires_at)` PARTIAL INDEX `WHERE revoked_at IS NULL` |
| 6 | `templates` | `Template` | UUIDv7 | `project_id` → `projects(id)` [CASCADE]<br>`current_version_id` → `template_versions(id)` [SET NULL] | `is_active` | `(slug)` UNIQUE, `(project_id)` FK |
| 7 | `template_versions` | `TemplateVersion` | UUIDv7 | `template_id` → `templates(id)` [CASCADE] | None | `(template_id, version)` UNIQUE, `(template_id)` FK |
| 8 | `field_mappings` | `FieldMapping` | UUIDv7 | `template_id` → `templates(id)` [CASCADE] | None | `(template_id, placeholder)` UNIQUE, `(template_id)` FK |
| 9 | `data_connections` | `DataConnection` | UUIDv7 | None | None | PK |
| 10 | `datasets` | `Dataset` | UUIDv7 | `data_connection_id` → `data_connections(id)` [RESTRICT] | None | `(data_connection_id)` FK |
| 11 | `template_datasets` | `TemplateDataset` | UUIDv7 | `template_id` → `templates(id)` [CASCADE]<br>`dataset_id` → `datasets(id)` [RESTRICT] | None | `(template_id, alias)` UNIQUE, `(template_id)` FK, `(dataset_id)` FK |
| 12 | `api_keys` | `ApiKey` | UUIDv7 | `project_id` → `projects(id)` [CASCADE] | `is_active` | `(project_id)` FK, `(key_hash)` INDEX |
| 13 | `generation_logs` | `GenerationLog` | UUIDv7 | `template_id` → `templates(id)` [SET NULL]<br>`template_version_id` → `template_versions(id)` [SET NULL]<br>`api_key_id` → `api_keys(id)` [SET NULL] | None | `(created_at DESC)` INDEX, FK indexes |
| 14 | `documents` | `Document` | UUIDv7 | `template_id` → `templates(id)` [SET NULL] | None | `(document_ref)` UNIQUE, `(template_id)` FK |
| 15 | `document_versions` | `DocumentVersion` | UUIDv7 | `document_id` → `documents(id)` [CASCADE]<br>`template_version_id` → `template_versions(id)` [SET NULL]<br>`generation_log_id` → `generation_logs(id)` [SET NULL] | None | `(document_id, version)` UNIQUE, FK indexes |
</authoritative_table_catalog>

---

<domain_persistence_matrix>
## 4. 🛡️ Clean Architecture Persistence Matrix (Domain POCO → PostgreSQL Column)

The Persistence Layer (`SmkDoc.Infrastructure`) bridges the pure domain model (`SmkDoc.Domain`) to PostgreSQL via EF Core 10 Fluent API configurations. No domain classes leak database or EF Core dependencies.

### 4.1 Domain Value Object Persistence Mappings

| Domain Value Object | Target Table & Column | SQL Primitive Column Type | EF Core Conversion (`.HasConversion()`) |
|---|---|---|---|
| `CompanyName` | `companies.name` | `VARCHAR(200)` | `n => n.Value, v => CompanyName.Create(v)` |
| `ProjectName` | `projects.name` | `VARCHAR(200)` | `n => n.Value, v => ProjectName.Create(v)` |
| `TemplateName` | `templates.name` | `VARCHAR(100)` | `v => v.Value, v => TemplateName.Create(v)` |
| `TemplateSlug` | `projects.slug`, `templates.slug` | `VARCHAR(100)` | `s => s.Value, v => new TemplateSlug(v)` |
| `EmailAddress` | `users.email` | `VARCHAR(255)` | `m => m.Value, v => EmailAddress.Create(v)` |
| `Sha256Hash` | `refresh_tokens.token_hash`<br>`api_keys.key_hash`<br>`generation_logs.payload_hash_sha256` | `VARCHAR(64)`<br>`VARCHAR(255)`<br>`VARCHAR(64)` | `h => h.Value, v => new Sha256Hash(v)` |
| `ApiKeyName` | `api_keys.name` | `VARCHAR(100)` | `v => v.Value, v => ApiKeyName.Create(v)` |
| `ExpirationPolicy` | `api_keys.expires_at` | `TIMESTAMPTZ` | `v => v.ExpiresAt, v => ExpirationPolicy.FromExisting(v)` |
| `DocumentReference` | `documents.document_ref` | `VARCHAR(100)` | `d => d.Value, v => DocumentReference.Create(v)` |
| `ConnectionName` | `data_connections.name` | `VARCHAR(100)` | `v => v.Value, v => ConnectionName.Create(v)` |
| `DatasetName` | `datasets.name` | `VARCHAR(200)` | `v => v.Value, v => DatasetName.Create(v)` |
| `DatasetAlias` | `field_mappings.dataset_alias`<br>`template_datasets.alias` | `VARCHAR(50)` | `a => a.Value, v => DatasetAlias.Create(v)` |

### 4.2 Smart Enum Persistence Mappings

All Smart Enums derive from `Enumeration` with O(1) static dictionary lookup:

| Smart Enum | Target Table & Column | Storage Type | EF Core Conversion | Default Value |
|---|---|---|---|---|
| `SystemRole` | `users.system_role` | `VARCHAR(30)` | `r => r.Name, v => SystemRole.FromDisplayName<SystemRole>(v)` | `'Member'` |
| `RoleType` | `user_project_roles.role` | `VARCHAR(20)` | `r => r.Name, v => RoleType.FromDisplayName<RoleType>(v)` | None (Required) |
| `ApiKeyScope` | `api_keys.scope` | `VARCHAR(20)` | `s => s.Name, v => ApiKeyScope.FromDisplayName<ApiKeyScope>(v)` | `'ReadWrite'` |
| `TemplateVersionStatus` | `template_versions.status` | `INTEGER` | `s => s.Id, v => TemplateVersionStatus.FromValue<TemplateVersionStatus>(v)` | `0` (`Draft`) |
| `TemplateFormat` | `template_versions.file_format` | `VARCHAR(10)` | `f => f.Name, v => TemplateFormat.FromDisplayName<TemplateFormat>(v)` | `NULL` |
| `OutputFormat` | `generation_logs.output_format` | `VARCHAR(10)` | `f => f.Name, v => OutputFormat.FromDisplayName<OutputFormat>(v)` | `NULL` |
| `GenerationStatus` | `generation_logs.status` | `VARCHAR(20)` | `s => s.Name, v => GenerationStatus.FromName(v)` | `'Success'` |
| `DataSourceType` | `field_mappings.data_source_type` | `VARCHAR(20)` | `d => d.Value, v => DataSourceType.FromString(v)` | `'json'` |
| `DatabaseProvider` | `data_connections.provider` | `VARCHAR(50)` | `p => p.Name, v => Enumeration.FromDisplayName<DatabaseProvider>(v)` | None (Required) |

> **Note on `TemplateVersionStatus` Storage:** Unlike general string-based Smart Enums, `TemplateVersionStatus` is stored as `INTEGER` (`0 = Draft`, `1 = Published`, `2 = Archived`) in accordance with active EF Core database migration `WaveTwo_FullSchemaRebuild` and `AppDbContext.cs`.
</domain_persistence_matrix>

---

<indexing_and_concurrency_strategy>
## 5. 🚀 High-Throughput Indexing & Concurrency Strategy

### 5.1 Sequential UUIDv7 Identity vs UUIDv4 B-Tree Fragmentation
*   **The Invariant:** Primary keys are strictly generated as time-ordered **UUIDv7** by `BaseEntity` (`Guid.CreateVersion7()`).
*   **PostgreSQL B-Tree Benefit:** Standard random UUIDv4 causes continuous page-splitting across the entire depth of the B-Tree index, leading to index bloat and high disk I/O. UUIDv7 contains a 48-bit millisecond unix timestamp prefix, guaranteeing monotonic index append locality, reducing cache thrashing, and keeping index write operations fast.

### 5.2 High-Performance Partial (Filtered) Indexes
PostgreSQL partial indexes index only relevant subsets of rows, saving substantial disk space and accelerating hot-path queries:
*   **Active Refresh Tokens Index:**
    ```sql
    CREATE INDEX idx_refresh_tokens_user_expires 
    ON refresh_tokens(user_id, expires_at) 
    WHERE revoked_at IS NULL;
    ```
    Allows token validation queries (`WHERE user_id = @id AND revoked_at IS NULL`) to completely bypass revoked token historical rows.

### 5.3 Multi-Tenant Isolation Composite Indexes
*   **Tenant Scoping Invariant:** Every query in the application filters by tenant context (`ProjectId`).
*   **Critical Composite Keys:**
    *   `projects`: `(slug)` UNIQUE
    *   `templates`: `(slug)` UNIQUE + `(project_id)` FK Index
    *   `template_versions`: `(template_id, version)` UNIQUE
    *   `field_mappings`: `(template_id, placeholder)` UNIQUE
    *   `template_datasets`: `(template_id, alias)` UNIQUE
    *   `generation_logs`: `(created_at DESC)` Index for chronological log paging

### 5.4 Optimistic Concurrency Token Configuration
*   **PostgreSQL `xmin` Token:** Configured in EF Core Fluent API for state-mutating aggregates:
    ```csharp
    builder.UseXminAsConcurrencyToken();
    ```
    EF Core automatically includes `WHERE xmin = @concurrencyToken` during updates and throws `DbUpdateConcurrencyException` upon collisions, mapped directly to HTTP 409 Conflict.
</indexing_and_concurrency_strategy>

---

<authoritative_ddl_spec>
## 6. 🗄️ Authoritative DDL Specification (PostgreSQL 16+)

```sql
-- ============================================================================
-- 0. MULTI-TENANCY & AUTHENTICATION CLUSTER
-- ============================================================================

CREATE TABLE companies (
    id         UUID PRIMARY KEY, -- UUIDv7 Generated by C# Application
    name       VARCHAR(200) NOT NULL,
    is_active  BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE TABLE projects (
    id         UUID PRIMARY KEY,
    company_id UUID NOT NULL REFERENCES companies(id) ON DELETE CASCADE,
    name       VARCHAR(200) NOT NULL,
    slug       VARCHAR(100) UNIQUE NOT NULL,
    is_active  BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);
CREATE INDEX idx_projects_company_id ON projects(company_id);

CREATE TABLE users (
    id            UUID PRIMARY KEY,
    email         VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    first_name    VARCHAR(100) NOT NULL,
    last_name     VARCHAR(100) NOT NULL,
    system_role   VARCHAR(30) NOT NULL DEFAULT 'Member', -- 'SuperAdmin', 'Member', 'Viewer'
    is_active     BOOLEAN NOT NULL DEFAULT true,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ
);

CREATE TABLE refresh_tokens (
    id                     UUID PRIMARY KEY,
    user_id                UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash             VARCHAR(64) UNIQUE NOT NULL,
    expires_at             TIMESTAMPTZ NOT NULL,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_at             TIMESTAMPTZ,
    replaced_by_token_hash VARCHAR(64)
);
CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_user_expires ON refresh_tokens(user_id, expires_at) WHERE revoked_at IS NULL;

CREATE TABLE user_project_roles (
    user_id    UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    role       VARCHAR(20) NOT NULL, -- 'Admin', 'Editor', 'Viewer'
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, project_id)
);
CREATE INDEX idx_user_project_roles_project_id ON user_project_roles(project_id);


-- ============================================================================
-- 1. TEMPLATE CATALOG & MAPPING CLUSTER
-- ============================================================================

CREATE TABLE templates (
    id                 UUID PRIMARY KEY,
    project_id         UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    name               VARCHAR(100) NOT NULL,
    slug               VARCHAR(100) UNIQUE NOT NULL,
    category           VARCHAR(50),
    is_active          BOOLEAN NOT NULL DEFAULT true,
    current_version_id UUID, -- Foreign Key to template_versions added after table creation
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_templates_project_id ON templates(project_id);

CREATE TABLE template_versions (
    id                UUID PRIMARY KEY,
    template_id       UUID NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    version           INTEGER NOT NULL,
    storage_key       VARCHAR(500) NOT NULL,
    status            INTEGER NOT NULL DEFAULT 0, -- 0: Draft, 1: Published, 2: Archived
    file_format       VARCHAR(10),                -- 'Html', 'Word', 'Excel'
    data_schema       JSONB,                      -- JSON Schema Draft-07
    sample_payload    JSONB,                      -- Sample mock payload for editor testing
    mappings_snapshot TEXT,                       -- JSON string snapshot of FieldMappings
    commit_message    VARCHAR(500),
    created_by        VARCHAR(100),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(template_id, version)
);
CREATE INDEX idx_template_versions_template_id ON template_versions(template_id);

-- Deferred circular foreign key for current active version
ALTER TABLE templates 
    ADD CONSTRAINT fk_templates_current_version_id 
    FOREIGN KEY (current_version_id) 
    REFERENCES template_versions(id) 
    ON DELETE SET NULL;

CREATE TABLE field_mappings (
    id               UUID PRIMARY KEY,
    template_id      UUID NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    placeholder      VARCHAR(100) NOT NULL,
    data_source_type VARCHAR(20) NOT NULL DEFAULT 'json', -- 'json', 'sql', 'expression'
    source_path      VARCHAR(200) NOT NULL,
    dataset_alias    VARCHAR(50),
    result_path      VARCHAR(300),
    math_expression  VARCHAR(500),
    label            VARCHAR(200) NOT NULL,
    required         BOOLEAN NOT NULL DEFAULT false,
    default_value    TEXT,
    transform        VARCHAR(50),
    sort_order       INTEGER NOT NULL DEFAULT 0,
    UNIQUE(template_id, placeholder)
);
CREATE INDEX idx_field_mappings_template_id ON field_mappings(template_id);


-- ============================================================================
-- 2. EXTERNAL DATASOURCES & SQL DATASETS CLUSTER
-- ============================================================================

CREATE TABLE data_connections (
    id                          UUID PRIMARY KEY,
    name                        VARCHAR(100) NOT NULL,
    provider                    VARCHAR(50) NOT NULL, -- 'PostgreSql', 'SqlServer', 'MySql', 'Oracle'
    encrypted_connection_string TEXT NOT NULL,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ
);

CREATE TABLE datasets (
    id                 UUID PRIMARY KEY,
    name               VARCHAR(200) NOT NULL,
    description        VARCHAR(500),
    data_connection_id UUID NOT NULL REFERENCES data_connections(id) ON DELETE RESTRICT,
    sql_query          TEXT NOT NULL,
    cache_seconds      INTEGER NOT NULL DEFAULT 0,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMPTZ
);
CREATE INDEX idx_datasets_data_connection_id ON datasets(data_connection_id);

CREATE TABLE template_datasets (
    id          UUID PRIMARY KEY,
    template_id UUID NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    dataset_id  UUID NOT NULL REFERENCES datasets(id) ON DELETE RESTRICT,
    alias       VARCHAR(50) NOT NULL,
    sort_order  INTEGER NOT NULL DEFAULT 0,
    UNIQUE(template_id, alias)
);
CREATE INDEX idx_template_datasets_template_id ON template_datasets(template_id);
CREATE INDEX idx_template_datasets_dataset_id ON template_datasets(dataset_id);


-- ============================================================================
-- 3. SECURITY & AUDIT TRAIL CLUSTER
-- ============================================================================

CREATE TABLE api_keys (
    id           UUID PRIMARY KEY,
    project_id   UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    name         VARCHAR(100) NOT NULL,
    caller_app   VARCHAR(50) NOT NULL,
    key_hash     VARCHAR(255) NOT NULL,
    expires_at   TIMESTAMPTZ, -- Expiration policy timestamp
    scope        VARCHAR(20) NOT NULL DEFAULT 'ReadWrite', -- 'ReadOnly', 'ReadWrite'
    is_active    BOOLEAN NOT NULL DEFAULT true,
    last_used_at TIMESTAMPTZ,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_api_keys_project_id ON api_keys(project_id);
CREATE INDEX idx_api_keys_key_hash ON api_keys(key_hash);

CREATE TABLE generation_logs (
    id                  UUID PRIMARY KEY,
    template_id         UUID REFERENCES templates(id) ON DELETE SET NULL,
    template_version_id UUID REFERENCES template_versions(id) ON DELETE SET NULL,
    api_key_id          UUID REFERENCES api_keys(id) ON DELETE SET NULL,
    caller_app          VARCHAR(50),
    trigger_source      VARCHAR(20),
    input_data          JSONB,
    payload_hash_sha256 VARCHAR(64),
    output_key          VARCHAR(500),
    output_format       VARCHAR(10),
    file_size_bytes     BIGINT,
    page_count          INTEGER,
    duration_ms         INTEGER NOT NULL,
    status              VARCHAR(20) NOT NULL, -- 'Success', 'Failed', 'Timeout'
    error_msg           TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_generation_logs_template_id ON generation_logs(template_id);
CREATE INDEX idx_generation_logs_template_version_id ON generation_logs(template_version_id);
CREATE INDEX idx_generation_logs_api_key_id ON generation_logs(api_key_id);
CREATE INDEX idx_generation_logs_created_at_desc ON generation_logs(created_at DESC);


-- ============================================================================
-- 4. LEGAL AUDIT TRAIL & DOCUMENT ARCHIVE CLUSTER
-- ============================================================================

CREATE TABLE documents (
    id           UUID PRIMARY KEY,
    document_ref VARCHAR(100) UNIQUE NOT NULL,
    template_id  UUID REFERENCES templates(id) ON DELETE SET NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_documents_template_id ON documents(template_id);

CREATE TABLE document_versions (
    id                  UUID PRIMARY KEY,
    document_id         UUID NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    version             INTEGER NOT NULL,
    template_version_id UUID REFERENCES template_versions(id) ON DELETE SET NULL,
    generation_log_id   UUID REFERENCES generation_logs(id) ON DELETE SET NULL,
    change_note         VARCHAR(500),
    created_by          VARCHAR(100),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(document_id, version)
);
CREATE INDEX idx_document_versions_document_id ON document_versions(document_id);
CREATE INDEX idx_document_versions_template_version_id ON document_versions(template_version_id);
CREATE INDEX idx_document_versions_generation_log_id ON document_versions(generation_log_id);
```
</authoritative_ddl_spec>

---

<jsonb_schema_contracts>
## 7. 📋 JSONB Schema Contracts & Strict Payloads

PostgreSQL `JSONB` columns store structured documents with binary indexing and validation capabilities:

### 7.1 `template_versions.data_schema` (JSON Schema Draft-07)
Stores the authoritative structural schema against which incoming generation payloads are strictly validated before rendering:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "InvoicePayload",
  "type": "object",
  "required": ["invoiceNumber", "issueDate", "customer", "items"],
  "properties": {
    "invoiceNumber": { "type": "string", "minLength": 1 },
    "issueDate": { "type": "string", "format": "date-time" },
    "customer": {
      "type": "object",
      "required": ["name", "taxId"],
      "properties": {
        "name": { "type": "string" },
        "taxId": { "type": "string" }
      }
    },
    "items": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["description", "quantity", "unitPrice"],
        "properties": {
          "description": { "type": "string" },
          "quantity": { "type": "number", "minimum": 1 },
          "unitPrice": { "type": "number", "minimum": 0 }
        }
      }
    }
  }
}
```

### 7.2 `generation_logs.input_data` (Sanitized Request Payload Snapshot)
Stores a complete, sanitized snapshot of the generation parameters used to produce the document for regulatory auditing:

```json
{
  "invoiceNumber": "INV-2026-0091",
  "issueDate": "2026-10-10T02:00:00Z",
  "customer": {
    "name": "Acme Corporation",
    "taxId": "0105558123456"
  },
  "items": [
    { "description": "Cloud Document Server License", "quantity": 1, "unitPrice": 45000.00 }
  ],
  "_metadata": {
    "callerApp": "ERP-Billing",
    "ipAddress": "192.168.1.50"
  }
}
```
</jsonb_schema_contracts>

---

<db_maintenance_and_migration_rules>
## 8. 🔄 Living SSoT & Database Migration Governance

> **[DATABASE_MAINTENANCE_MANDATE]:** When altering schema, adding tables, or mutating column mappings:
> 1. **Code-First SSoT:** All schema alterations MUST originate in `SmkDoc.Domain` entities and `SmkDoc.Infrastructure` Fluent API configurations (`IEntityTypeConfiguration<T>`).
> 2. **Migration Generation:** Execute:
>    ```bash
>    dotnet ef migrations add <DescriptiveMigrationName> --project backend-v2/src/SmkDoc.Infrastructure --startup-project backend-v2/src/SmkDoc.Api
>    ```
> 3. **Non-Destructive Migrations:** Never execute destructive table drops in active production branches. Deprecated columns must be staged via soft-retire cycles.
> 4. **Synchronize Documentation:** Whenever an entity or relation is added, update Section 3 Table Catalog, Section 4 Value Object Matrix, and Section 6 DDL in this document.
</db_maintenance_and_migration_rules>
