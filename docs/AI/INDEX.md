# [INDEX-001]: INDEX.md — Master Documentation Switchboard & Cognitive Intent Router

<context>
Scope: Master Documentation Switchboard, Cognitive Intent Router & LLM Working Set Budgeting
Target: SmkDoc Full-Stack System (`backend-v2/` & `frontend-v2/`)
Authority: Absolute SSoT Router (Direct lookup to authoritative leaf specifications)
Boundary: Master documentation switchboard. Prohibit technical implementations, code snippets, or task tracking.
</context>

---

## 1. High-Salience Cognitive Intent Router

Use this matrix to route developer or AI agent intent directly to the authoritative leaf specification:

| Incoming Intent / Task Scope | Authoritative Leaf Specification | Key Concepts / Invariants |
|---|---|---|
| **Clean Architecture, Dependencies & Bounded Contexts** | [`00_architecture_core/dependency_and_folder_rules.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/00_architecture_core/dependency_and_folder_rules.md) | Inversion flow (Domain <- App <- Infra/Api), 4 Bounded Contexts, 5-File CQRS slice |
| **Anti-Patterns, Memory Safety & Security Firewall** | [`00_architecture_core/anti_pattern_firewall.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/00_architecture_core/anti_pattern_firewall.md) | Zero-LOH streaming, DbContext concurrency, async blocking, SSRF, timing attacks |
| **Aggregate Roots, Value Objects, Smart Enums & Guards** | [`01_clean_arch_patterns/01_domain_layer.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/01_clean_arch_patterns/01_domain_layer.md) | Encapsulation, BaseEntity<TId>, SSoT Factory, 12 Strongly-Typed IDs & 9 Smart Enums |
| **Commands, Queries, UseCases & PEMR Pipeline** | [`01_clean_arch_patterns/02_application_layer.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/01_clean_arch_patterns/02_application_layer.md) | Positional record requests, IUseCase, PEMR pipeline, IUnitOfWork, Application Exceptions |
| **EF Core Mappings, Repositories & Tenancy Filter** | [`01_clean_arch_patterns/03_infrastructure_layer.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/01_clean_arch_patterns/03_infrastructure_layer.md) | IEntityTypeConfiguration, ID Value Converters, JSONB, BaseRepository, Global Query Filter |
| **Thin Controllers, Envelopes & RFC 9457 Errors** | [`01_clean_arch_patterns/04_api_presentation_layer.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/01_clean_arch_patterns/04_api_presentation_layer.md) | Thin controllers (<= 5 lines), ApiResponse<T>, [Idempotent], GlobalExceptionHandler |
| **Unit Testing SSoT, CreateSut() & Invariant Tests** | [`01_clean_arch_patterns/05_testing_archetypes.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/01_clean_arch_patterns/05_testing_archetypes.md) | Zero disk/network IO, CreateSut(), FakeTimeProvider, Fluent *Builder, Aggregate tests |
| **Next.js App Router, Route Proxy & Live Preview** | [`02_frontend_patterns/01_app_router_and_data_fetching.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/02_frontend_patterns/01_app_router_and_data_fetching.md) | RSC by default, Server Proxy (/api/proxy/[...path]), useLivePreview debounce (800ms) |
| **Ice-White Tokens, CVA Primitives & Zod SSoT** | [`02_frontend_patterns/02_ui_component_conventions.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/02_frontend_patterns/02_ui_component_conventions.md) | Ice-White tokens, 0-4px compact geometry, CVA variants, Lucide icons, Vitest |
| **Dual-Auth, Tenancy Context & Project Scoping** | [`03_specialized_mechanics/multi_tenant_security_pipeline.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/03_specialized_mechanics/multi_tenant_security_pipeline.md) | Channel A (X-API-Key SHA-256) vs Channel B (JWT), ICurrentProjectContext, IExecutionContext |
| **Gotenberg, OpenXML DrawingML & Thai Formatting** | [`03_specialized_mechanics/document_rendering_mechanics.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/03_specialized_mechanics/document_rendering_mechanics.md) | Gotenberg 8 direct streaming, DrawingML non-zero ID fix, ClosedXML A4 fit, ThaiDataTransformer |

---

## 2. LLM Working Set Budgeting (Selective Attention)

To eliminate attention leakage and prevent Lost-in-the-Middle, agents MUST load strictly the minimal relevant slice:

```
[For Domain Entity / VO Task]       >> 00_architecture_core/ + 01_clean_arch_patterns/01_domain_layer.md
[For UseCase / Application Task]    >> 00_architecture_core/ + 01_clean_arch_patterns/02_application_layer.md
[For EF Core / Database Task]       >> 00_architecture_core/ + 01_clean_arch_patterns/03_infrastructure_layer.md
[For API / Controller Task]         >> 00_architecture_core/ + 01_clean_arch_patterns/04_api_presentation_layer.md
[For Unit Test Task]                >> 00_architecture_core/ + 01_clean_arch_patterns/05_testing_archetypes.md
[For Frontend UI / Page Task]       >> 02_frontend_patterns/01_app_router_and_data_fetching.md + 02_ui_component_conventions.md
[For Auth / Multi-Tenancy Task]     >> 03_specialized_mechanics/multi_tenant_security_pipeline.md
[For PDF / Office Rendering Task]   >> 03_specialized_mechanics/document_rendering_mechanics.md
```
*Total active context per task remains strictly $\le 1,500$ tokens regardless of total project size.*

---

## 3. Clean Architecture Topology Map

```
docs/AI/
├── INDEX.md                                    # Master Documentation Switchboard
│
├── 00_architecture_core/                       # Architecture Rules & Firewall
│   ├── dependency_and_folder_rules.md          # Clean Architecture dependencies & 4 Bounded Contexts
│   └── anti_pattern_firewall.md                # 8 Fatal production traps (Zero-LOH, DbContext, Async, SSRF)
│
├── 01_clean_arch_patterns/                     # Backend C# 13 Clean Architecture Patterns
│   ├── 01_domain_layer.md                      # Aggregate Root, Child Entity, VO, Smart Enum, SSoT Catalog
│   ├── 02_application_layer.md                 # Command/Query Record, IUseCase, PEMR Pipeline, UnitOfWork
│   ├── 03_infrastructure_layer.md              # EF Core IEntityTypeConfiguration, ID Converters, Global Filter
│   ├── 04_api_presentation_layer.md            # Thin Controller, ApiResponse<T>, [Idempotent], ProblemDetails
│   └── 05_testing_archetypes.md                 # SSoT Unit Test, CreateSut(), FakeTimeProvider, Fluent Builder
│
├── 02_frontend_patterns/                       # Frontend Next.js 15 / React Patterns
│   ├── 01_app_router_and_data_fetching.md      # RSC by default, Server Proxy (/api/proxy/[...path]), useLivePreview
│   └── 02_ui_component_conventions.md          # Ice-White tokens, 0-4px geometry, CVA variants, Zod SSoT, Vitest
│
└── 03_specialized_mechanics/                   # High-Complexity Subsystem Mechanics
    ├── multi_tenant_security_pipeline.md       # Dual-Auth (API Key / JWT), Tenancy Context, EF Global Filter
    └── document_rendering_mechanics.md         # Gotenberg microservice, OpenXML DrawingML fix, ThaiDataTransformer
```
