# DB_SCHEMA.md — PostgreSQL Relational Schema & Tables

> **Purpose:** Authoritative PostgreSQL schema definition, UUIDv7 PK types, foreign keys, and index strategy.  
> **Related Docs:** [ARCHITECTURE.md](ARCHITECTURE.md) (Persistence layer), [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) (Domain entities).

### ⚡ Quick-Lookup: Core Database Tables

| Table Name | Entity | Primary Key | Key Foreign Keys & Indexes |
|---|---|---|---|
| `companies` | `Company` | UUID | — |
| `projects` | `Project` | UUID | `company_id` → `companies(id)`, `slug` UNIQUE |
| `users` | `User` | UUID | `email` UNIQUE |
| `refresh_tokens` | `RefreshToken` | UUID | `user_id` → `users(id)`, `token_hash` UNIQUE, `(user_id, expires_at)` INDEX |
| `api_keys` | `ApiKey` | UUID | `project_id` → `projects(id)`, `key_hash` INDEX |
| `templates` | `Template` | UUID | `project_id`, `(project_id, slug)` UNIQUE |
| `template_versions` | `TemplateVersion` | UUID | `template_id` → `templates(id)`, `(template_id, version)` UNIQUE |
| `field_mappings` | `FieldMapping` | UUID | `template_id` → `templates(id)` |
| `generation_logs` | `GenerationLog` | UUID | `project_id`, `template_id`, `created_at` DESC INDEX |

---

## 🗄️ Database DDL (PostgreSQL — SDD v1.3)

```sql
-- 0. Multi-Tenancy & Auth
CREATE TABLE companies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE projects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL REFERENCES companies(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    slug VARCHAR(100) UNIQUE NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    system_role VARCHAR(30) DEFAULT 'Member', -- 'SuperAdmin', 'Member', 'Viewer'
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE refresh_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(64) UNIQUE NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    revoked_at TIMESTAMPTZ,
    replaced_by_token_hash VARCHAR(64)
);

CREATE INDEX idx_refresh_tokens_user_expires ON refresh_tokens (user_id, expires_at) WHERE revoked_at IS NULL;

CREATE TABLE user_project_roles (
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    project_id UUID REFERENCES projects(id) ON DELETE CASCADE,
    role VARCHAR(20) NOT NULL, -- 'Admin', 'Editor', 'Viewer'
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (user_id, project_id)
);

-- 1. Templates
CREATE TABLE templates (
    id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id         UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    name               VARCHAR(200) NOT NULL,
    slug               VARCHAR(100) UNIQUE NOT NULL,
    category           VARCHAR(50),
    is_active          BOOLEAN DEFAULT true,
    current_version_id UUID,
    created_at         TIMESTAMPTZ DEFAULT NOW(),
    updated_at         TIMESTAMPTZ DEFAULT NOW()
);

-- 2. Field Mappings
CREATE TABLE field_mappings (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id      UUID NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    placeholder      VARCHAR(100) NOT NULL,
    data_source_type VARCHAR(20) DEFAULT 'json',
    source_path      VARCHAR(200) NOT NULL,
    dataset_alias    VARCHAR(50),
    result_path      VARCHAR(300),
    math_expression  VARCHAR(500),
    label            VARCHAR(200) NOT NULL,
    required         BOOLEAN DEFAULT false,
    default_value    TEXT,
    transform        VARCHAR(50),
    sort_order       INTEGER DEFAULT 0,
    UNIQUE(template_id, placeholder)
);

-- 3. Template Versions
CREATE TABLE template_versions (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id       UUID NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    version           INTEGER NOT NULL,
    storage_key       VARCHAR(500) NOT NULL,
    status            INTEGER DEFAULT 0,
    file_format       VARCHAR(10),
    data_schema       JSONB,
    sample_payload    JSONB,
    mappings_snapshot TEXT,
    change_note       TEXT,
    commit_message    VARCHAR(500),
    created_by        VARCHAR(100),
    created_at        TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(template_id, version)
);

-- 4. API Keys
CREATE TABLE api_keys (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id   UUID REFERENCES projects(id) ON DELETE CASCADE,
    name         VARCHAR(100) NOT NULL,
    caller_app   VARCHAR(50)  NOT NULL,
    key_hash     VARCHAR(255) NOT NULL,
    is_active    BOOLEAN DEFAULT true,
    last_used_at TIMESTAMPTZ,
    created_at   TIMESTAMPTZ DEFAULT NOW()
);

-- 5. Generation Logs
CREATE TABLE generation_logs (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id           UUID REFERENCES templates(id) ON DELETE SET NULL,
    template_version_id   UUID REFERENCES template_versions(id) ON DELETE SET NULL,
    api_key_id            UUID REFERENCES api_keys(id) ON DELETE SET NULL,
    caller_app            VARCHAR(50),
    trigger_source        VARCHAR(20),
    input_data            JSONB,
    payload_hash_sha256   VARCHAR(64),
    output_key            VARCHAR(500),
    output_format         VARCHAR(10),
    file_size_bytes       BIGINT,
    page_count            INTEGER,
    duration_ms           INTEGER,
    status                VARCHAR(20) NOT NULL,
    error_msg             TEXT,
    created_at            TIMESTAMPTZ DEFAULT NOW()
);

-- 6. Documents (Legal Audit Trail Base)
CREATE TABLE documents (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_ref  VARCHAR(100) NOT NULL,
    template_id   UUID REFERENCES templates(id) ON DELETE SET NULL,
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(document_ref)
);

-- 6.1. Document Versions (Legal Audit Trail Snapshots)
CREATE TABLE document_versions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id         UUID NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    version             INTEGER NOT NULL,
    template_version_id UUID REFERENCES template_versions(id) ON DELETE SET NULL,
    generation_log_id   UUID REFERENCES generation_logs(id) ON DELETE SET NULL,
    change_note         TEXT,
    created_by          VARCHAR(100),
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(document_id, version)
);

-- 7. Data Connections (Datasources V2)
CREATE TABLE data_connections (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name         VARCHAR(200) NOT NULL,
    provider     VARCHAR(50) NOT NULL,
    encrypted_connection_string TEXT NOT NULL,
    created_at   TIMESTAMPTZ DEFAULT NOW(),
    updated_at   TIMESTAMPTZ
);

-- 8. Datasets
CREATE TABLE datasets (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name         VARCHAR(200) NOT NULL,
    description  TEXT,
    data_connection_id UUID NOT NULL REFERENCES data_connections(id) ON DELETE CASCADE,
    sql_query    TEXT NOT NULL,
    cache_seconds INTEGER DEFAULT 0,
    created_at   TIMESTAMPTZ DEFAULT NOW(),
    updated_at   TIMESTAMPTZ
);

-- 9. Template Datasets
CREATE TABLE template_datasets (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id  UUID NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    dataset_id   UUID NOT NULL REFERENCES datasets(id) ON DELETE CASCADE,
    alias        VARCHAR(50) NOT NULL,
    sort_order   INTEGER DEFAULT 0,
    UNIQUE(template_id, alias)
);
```

---

