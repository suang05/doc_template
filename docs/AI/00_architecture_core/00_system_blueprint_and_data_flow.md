# [BLUEPRINT-001]: 00_system_blueprint_and_data_flow.md — Master System Architecture & Data Flow SSoT

<context>
Scope: Supreme System Architecture Blueprint, C4 Container Topology, End-to-End Macroscopic Data Pipelines & 4 Bounded Contexts Collaboration Matrix
Target: Full-Stack (`backend-v2/` & `frontend-v2/`)
Authority: Supreme Architectural Blueprint & North Star SSoT for all Agents and Developers
Boundary: High-level system strategy, end-to-end data flows, container mesh, and architectural boundaries.
TriggerSignals: [SIGNAL_TOPOLOGY_MUTATION], [SIGNAL_STRUCTURE_MUTATION]
UpstreamKernel: AGENTS.md
</context>

---

## 1. System Identity, Mission & Architectural North Star

### 1.1 Core Mission
`smk-doc-server` is an enterprise-grade, high-throughput centralized document gateway ingesting structured JSON payloads and external database queries to produce deterministic outputs across three target formats: **PDF**, **DOCX**, and **XLSX**.

### 1.2 Benchmark Architecture & Competitive Parity
| Dimension | Legacy / Standard SaaS (SSRS, Jasper, Carbone) | SMK Document Server Canonical Target |
|---|---|---|
| **Process Model** | Embedded heavyweight runtime / LibreOffice child processes | 100% Stateless container pods (.NET 10 + Gotenberg 8 Chromium cluster) |
| **Thai Localization**| Text overlapping, broken tone marks, missing Buddhist Era | Centralized `ThaiDataTransformer` SSoT + Google Fonts Sarabun CSS injection |
| **Memory Architecture**| Multi-MB `byte[]` buffering causing Gen 2 Large Object Heap stalls | Stream-over-RAM direct pipe (`Stream.CopyToAsync`) into HTTP `Response.Body` |
| **Multi-Tenancy** | Single database per tenant or manual fragile `WHERE` clauses | Single-database schema with automated EF Core Global Query Filter on `ProjectId` |
| **Authentication** | Basic Auth or complex proprietary tokens | Dual-Channel: Channel A (M2M SHA-256 API Keys) & Channel B (Portal JWT) |

---

## 2. Enterprise C4 Container Architecture

The diagram below illustrates the macroscopic system topology and inward dependency boundaries across all 7 operational tiers:

```mermaid
graph TD
    subgraph Tier1["1. Consumers & Clients"]
        UI["Portal Web UI (Next.js 15 App Router / React 19)"]
        ERP["Enterprise Systems (ERP / CRM / Core Banking)"]
    end

    subgraph Tier2["2. Edge Security & Ingress Pipeline"]
        Ingress["Reverse Proxy / API Gateway (TLS Termination & Rate Limiting)"]
        DualAuth{"Dual-Channel Auth Router"}
    end

    subgraph Tier3["3. Presentation Gateway (SmkDoc.Api - .NET 10)"]
        ExceptionPipe["GlobalExceptionHandler (RFC 9457 ProblemDetails)"]
        SecurityHeaders["SecurityHeadersMiddleware"]
        IdempFilter["IdempotencyFilter (IETF 6-State SHA-256)"]
        Controllers["Thin HTTP Controllers (<= 5 lines per action)"]
    end

    subgraph Tier4["4. Application Orchestration (SmkDoc.Application)"]
        CQRS["Action-Centric UseCases (IUseCase<TCommand, TResult>)"]
        ThaiTrans["ThaiDataTransformer SSoT"]
        Ports["Application Ports & Interfaces (ITemplateRepo, IStorageService, IRenderEngine)"]
    end

    subgraph Tier5["5. Domain Core (SmkDoc.Domain - Pure C# 13 POCOs)"]
        Entities["Sealed Rich Aggregate Roots & Entities (UUIDv7)"]
        ValueObjects["Self-Validating Value Objects"]
        SmartEnums["Smart Enums (O(1) Static Dictionaries)"]
        DomainEvents["Immutable Domain Event Records"]
    end

    subgraph Tier6["6. Infrastructure Adapters (SmkDoc.Infrastructure)"]
        EF["EF Core 10 + Npgsql Repository Adapters"]
        Engines["Polymorphic Render Engines (Gotenberg / OpenXML / ClosedXML)"]
        MinioAdapter["MinIO S3 Storage Adapter"]
        GotenbergClient["Gotenberg HTTP Client (ResponseHeadersRead Streaming)"]
    end

    subgraph Tier7["7. External Infrastructure Mesh"]
        Postgres[("PostgreSQL 16+ Database")]
        Minio[("MinIO S3 Object Storage")]
        GotenbergCluster["Gotenberg 8 Chromium Cluster"]
        OTel["OpenTelemetry Collector & Prometheus"]
    end

    UI -->|"Channel B: Bearer JWT"| Ingress
    ERP -->|"Channel A: X-API-Key"| Ingress
    Ingress --> DualAuth
    DualAuth --> ExceptionPipe
    ExceptionPipe --> SecurityHeaders
    SecurityHeaders --> IdempFilter
    IdempFilter --> Controllers
    Controllers --> CQRS
    CQRS --> ThaiTrans
    CQRS --> Entities
    CQRS --> Ports
    Ports -.-> EF
    Ports -.-> Engines
    Ports -.-> MinioAdapter
    Ports -.-> GotenbergClient

    EF --> Postgres
    MinioAdapter --> Minio
    GotenbergClient --> GotenbergCluster
    Tier3 -.-> OTel
    Tier6 -.-> OTel
```

---

## 3. The 2 Macroscopic Execution Pipelines

### Pipeline A: Ephemeral Live Preview Pipeline (Stateless, 100% In-Memory)
Used by the Template Studio (`frontend-v2`) for instant visual feedback during template authoring.

```mermaid
sequenceDiagram
    autonumber
    actor Author as Template Designer
    participant Studio as Next.js Studio (Monaco Editor)
    participant Proxy as Next.js Server Proxy (/api/proxy)
    participant Api as SmkDoc.Api (Thin Controller)
    participant App as RenderPreviewUseCase (Application)
    participant Engine as HtmlTemplateEngine (Infrastructure)
    participant Gotenberg as Gotenberg 8 (Chromium)

    Author->>Studio: Edits HTML/Handlebars & Sample JSON
    Studio->>Proxy: POST /api/proxy/v1/documents/preview/{slug} (Debounced 800ms)
    Proxy->>Api: Forward with Session Tenant & Bearer Token
    Api->>App: ExecuteAsync(RenderPreviewCommand)
    App->>App: Validate payload line 1 via FluentValidation
    App->>Engine: RenderHtmlToPdfStreamAsync(mergedHtml)
    Engine->>Gotenberg: POST /forms/chromium/convert/html (Multipart stream)
    Gotenberg-->>Engine: Raw PDF Output Stream
    Engine-->>App: Pass-Through Stream
    App-->>Api: Stream Result
    Api-->>Proxy: HTTP 200 chunked stream (application/pdf, inline)
    Proxy-->>Studio: Pipe Stream
    Studio->>Author: Revoke old Blob URL & render new iframe in 300ms
```

* **Stateless Preview Invariants:**
  1. **Zero Database Mutations:** Preview never writes to PostgreSQL `documents` or `generation_logs`.
  2. **Zero Storage Persistence:** Preview never uploads bytes to MinIO S3.
  3. **Stream-over-RAM:** Payload flows directly from Gotenberg through .NET network buffers to the browser Blob URL without intermediate disk or `byte[]` RAM buffering.

---

### Pipeline B: High-Throughput Production Generation & Storage Delivery
Used by enterprise callers (ERP, Core Banking) to produce legally binding documents and audit trails.

```mermaid
sequenceDiagram
    autonumber
    actor Caller as Enterprise ERP System
    participant Edge as API Gateway & Dual-Auth
    participant Api as DocumentController (SmkDoc.Api)
    participant UseCase as GenerateDocumentUseCase (Application)
    participant Repo as DocumentRepository & EF Core (Infra)
    participant Extractor as DatasetExtractor (Integration)
    participant Engine as Polymorphic Engine Resolver
    participant Minio as MinIO S3 Storage Adapter
    participant DB as PostgreSQL 16+

    Caller->>Edge: POST /api/v1/documents/generate/{slug} + X-API-Key + Idempotency-Key
    Edge->>Edge: SHA-256 Hash Lookup -> Resolve ProjectId & Scopes
    Edge->>Api: Authorized Request with Ambient Context
    Api->>UseCase: ExecuteAsync(GenerateDocumentCommand)
    UseCase->>UseCase: Step 1 (Parse): Validate Command via FluentValidation
    UseCase->>Extractor: Step 2 (Extract): Resolve dynamic data from External DB / Payload
    Extractor-->>UseCase: Extracted JSON Dataset
    UseCase->>UseCase: Apply ThaiDataTransformer (Dates to BE, BahtText)
    UseCase->>Engine: Step 3 (Render): Resolve Engine (Gotenberg / OpenXML / ClosedXML)
    Engine-->>UseCase: Output Binary Document Stream
    UseCase->>Minio: Step 4 (Store): UploadStreamAsync(storageKey, stream)
    Minio-->>UseCase: S3 ETag & Storage Metadata
    UseCase->>Repo: Step 5 (Persist): Create Document Version & GenerationLog
    UseCase->>DB: unitOfWork.CommitAsync(ct)
    DB-->>UseCase: Transaction Committed
    UseCase->>UseCase: Step 6 (Events): Append DocumentCreatedDomainEvent
    UseCase-->>Api: Return DocumentGenerationResultDto
    Api-->>Caller: HTTP 201 Created with Location Header & Download Stream URL
```

---

## 4. The 4 Bounded Context Collaboration Matrix

The system is partitioned into 4 distinct Bounded Contexts under `backend-v2/src/SmkDoc.Application/Modules/`:

```
SmkDoc Bounded Contexts
├── Authoring Context         # Template lifecycle, versioning, field mapping, fonts
├── Rendering Context         # Document production, polymorphic engines, audit logs
├── Integration Context       # External databases, queries, datasets
└── IdentityAccess Context    # Tenants, projects, users, API keys, RBAC
```

### Collaboration & Interaction Matrix
| Bounded Context | Core Entities (Domain) | Upstream Dependencies | Downstream Consumers | Shared Boundary Contracts |
|---|---|---|---|---|
| **`Authoring`** | `Template`, `TemplateVersion`, `FieldMapping`, `Font` | `IdentityAccess` (Project scoping) | `Rendering` (Template resolution) | `TemplateId`, `TemplateSlug`, `TemplateVersionId`, `TemplatePublishedDomainEvent` |
| **`Rendering`** | `Document`, `DocumentVersion`, `GenerationLog` | `Authoring` (Templates), `Integration` (Datasets) | External Callers, Portal UI | `DocumentId`, `RenderEngineType`, `OutputFormat`, `Stream` |
| **`Integration`**| `DataConnection`, `Dataset` | `IdentityAccess` (Project scoping) | `Rendering` (Data extraction) | `DataConnectionId`, `DatasetId`, `ExtractionStrategy` |
| **`IdentityAccess`**| `Company`, `Project`, `User`, `ApiKey` | Edge Authentication Middleware | All other Contexts (Tenant filtering) | `ProjectId`, `CompanyId`, `UserId`, `ApiKeyId`, `ICurrentProjectContext` |

---

## 5. Strategic Architectural Decisions (ADRs)

### ADR-01: Gotenberg Chromium Cluster over Embedded LibreOffice
* **Context:** Document servers often rely on headless LibreOffice (`soffice`) for PDF conversion, which is prone to memory leaks, process crashes, and font shifting between OS environments.
* **Decision:** Decouple PDF generation to an external Gotenberg 8 microservice running headless Chromium. HTML/CSS is treated as the first-class print medium (`@page { size: A4 portrait; margin: 15mm; }`).
* **Consequence:** 100% deterministic layout matching the browser preview, zero host process crashes, independent horizontal auto-scaling of rendering workers.

### ADR-02: Stream-over-RAM Direct Egress (Zero-LOH Paradigm)
* **Context:** Any memory allocation $\ge 85,000$ bytes lands on the .NET Large Object Heap (LOH). Buffering multi-MB PDFs into `byte[]` arrays causes Gen 2 GC stalls under heavy load.
* **Decision:** Gotenberg HTTP responses and MinIO S3 streams MUST pipe directly into `HttpResponse.Body` using bounded buffers ($81,920$ bytes). Prohibit `MemoryStream.ToArray()` across all production paths.
* **Consequence:** Constant memory footprint regardless of whether generating a 100KB receipt or a 50MB report.

### ADR-03: Ambient Multi-Tenancy via EF Core Global Query Filter
* **Context:** Relying on developers to manually remember `WHERE project_id = @id` on every query inevitably leads to catastrophic IDOR data leakage across tenants.
* **Decision:** All tenant-scoped entities implement `IMustHaveProject`. `AppDbContext.OnModelCreating` applies an automated `HasQueryFilter` bound to `ICurrentProjectContext`.
* **Consequence:** Cross-tenant queries are blocked at the SQL compilation level. Standard repository queries (`db.Templates.ToListAsync()`) automatically isolate data to the active project context.

### ADR-04: Direct Action-Centric `IUseCase` over MediatR Indirection
* **Context:** MediatR introduces runtime reflection, object allocation for pipeline behaviors, and hides call stacks, making navigation and debugging cumbersome.
* **Decision:** Every business action is a single-responsibility class implementing `IUseCase<TCommand, TResultDto>`. Controllers inject use cases directly via C# 13 Primary Constructors.
* **Consequence:** Explicit compile-time type verification, zero-allocation handler resolution, transparent call stacks, and straightforward unit testing via class-scoped `CreateSut()`.

### ADR-05: Centralized ThaiDataTransformer & Standardized Thai Typography
* **Context:** Thai typography requires proper handling of floating vowels, tone mark rendering, Buddhist Era year offsets (+543), and Thai Baht currency text (`BahtText`). Inline Handlebars logic leads to inconsistent document outputs.
* **Decision:** All localized data formatting is centralized in `ThaiDataTransformer`. All HTML templates inject Google Fonts Sarabun and standard CSS rules for Thai word breaking (`word-break: break-word; line-break: loose;`).
* **Consequence:** 100% consistent, legally compliant Thai enterprise documents across all output channels.
