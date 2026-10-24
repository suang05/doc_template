# [INDEX-001]: INDEX.md — Architectural Documentation Directory Manifest

<context>
Scope: Human-Facing Architectural Directory Manifest & File Catalog
Target: SmkDoc Full-Stack System (`backend-v2/` & `frontend-v2/`)
Authority: Navigation Manifest. Absolute SSoT Cognitive Router resides in AGENTS.md.
Boundary: Directory layout and human navigation only. AI Agents MUST use AGENTS.md.
TriggerSignals: [SIGNAL_STRUCTURE_MUTATION]
UpstreamKernel: AGENTS.md
</context>

> [!IMPORTANT]
> **For AI Agents:** The absolute Single Source of Truth (SSoT) for Cognitive Intent Routing,
> Token Budgeting, and Compound Trigger Satellites is centralized directly in `AGENTS.md` (`<compound_context_router>`).
> Do NOT read this file during agent workflows; route directly via `AGENTS.md`.

---

## 1. Documentation Topology Tree

```
docs/AI/
├── INDEX.md                                              # Architectural Directory Manifest (This file)
│
├── 00_architecture_core/                                 # Supreme Blueprint, Architecture Rules & Traps
│   ├── 00_system_blueprint_and_data_flow.md             # SUPREME SSoT: Master Blueprint, C4 Architecture & 2 Pipelines
│   ├── dependency_and_folder_rules.md                   # Clean Architecture dependencies & 4 Bounded Contexts
│   └── anti_pattern_firewall.md                         # 8 Fatal production traps (Zero-LOH, DbContext, Async, SSRF)
│
├── 01_clean_arch_patterns/                               # Backend C# 13 Clean Architecture Patterns
│   ├── 01_domain_layer.md                               # Aggregate Root, Child Entity, VO, Smart Enum, SSoT Catalog
│   ├── 02_application_layer.md                          # Command/Query Record, IUseCase, PEMR Pipeline, UnitOfWork
│   ├── 03_infrastructure_layer.md                       # EF Core IEntityTypeConfiguration, ID Converters, Global Filter
│   ├── 04_api_presentation_layer.md                     # Thin Controller, ApiResponse<T>, [Idempotent], ProblemDetails
│   └── 05_testing_archetypes.md                         # SSoT Unit Test, CreateSut(), FakeTimeProvider, Fluent Builder
│
├── 02_frontend_patterns/                                 # Frontend Next.js 15 / React Patterns
│   ├── 01_app_router_and_data_fetching.md               # RSC by default, Server Proxy (/api/proxy/[...path]), useLivePreview
│   └── 02_ui_component_conventions.md                   # Ice-White tokens, 0-4px geometry, CVA variants, Zod SSoT, Vitest
│
└── 03_specialized_mechanics/                             # High-Complexity Subsystem Mechanics
    ├── multi_tenant_security_pipeline.md                # Dual-Auth (API Key / JWT), Tenancy Context, EF Global Filter
    └── document_rendering_mechanics.md                  # Gotenberg microservice, OpenXML DrawingML fix, ThaiDataTransformer
```

---

## 2. Directory & Specification Catalog

| Specification File | Scope / Focus | Governed Subsystems |
|---|---|---|
| [`00_architecture_core/00_system_blueprint_and_data_flow.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/00_architecture_core/00_system_blueprint_and_data_flow.md) | **Supreme North Star SSoT:** C4 topology, 2 Macroscopic Pipelines, 4 Contexts, ADRs | Full-Stack |
| [`00_architecture_core/dependency_and_folder_rules.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/00_architecture_core/dependency_and_folder_rules.md) | Clean Architecture concentric layers & 4 Bounded Context feature slices | `backend-v2/src/` |
| [`00_architecture_core/anti_pattern_firewall.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/00_architecture_core/anti_pattern_firewall.md) | 8 Fatal production interceptors (LOH, DbContext, Async deadlocks, SSRF) | Full-Stack |
| [`01_clean_arch_patterns/01_domain_layer.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/01_clean_arch_patterns/01_domain_layer.md) | Aggregate Roots, Child Entities, Value Objects, Smart Enums, Factory SSoT | `SmkDoc.Domain/` |
| [`01_clean_arch_patterns/02_application_layer.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/01_clean_arch_patterns/02_application_layer.md) | Positional request records, IUseCase, PEMR pipeline, Unit of Work | `SmkDoc.Application/` |
| [`01_clean_arch_patterns/03_infrastructure_layer.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/01_clean_arch_patterns/03_infrastructure_layer.md) | EF Core entity configurations, ID converters, JSONB, Global Query Filter | `SmkDoc.Infrastructure/` |
| [`01_clean_arch_patterns/04_api_presentation_layer.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/01_clean_arch_patterns/04_api_presentation_layer.md) | Thin controllers (<= 5 lines), ApiResponse<T>, RFC 9457 ProblemDetails | `SmkDoc.Api/` |
| [`01_clean_arch_patterns/05_testing_archetypes.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/01_clean_arch_patterns/05_testing_archetypes.md) | CreateSut() SSoT, FakeTimeProvider, Fluent Builders, Invariant Tests | `SmkDoc.Tests/` |
| [`02_frontend_patterns/01_app_router_and_data_fetching.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/02_frontend_patterns/01_app_router_and_data_fetching.md) | Next.js 15 App Router, RSC boundaries, Route Proxy, useLivePreview | `frontend-v2/src/` |
| [`02_frontend_patterns/02_ui_component_conventions.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/02_frontend_patterns/02_ui_component_conventions.md) | Ice-White design tokens, CVA primitives, Lucide icons, Zod SSoT schemas | `frontend-v2/src/` |
| [`03_specialized_mechanics/multi_tenant_security_pipeline.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/03_specialized_mechanics/multi_tenant_security_pipeline.md) | Dual-channel authentication (API Key SHA-256 vs JWT), Project Context | Full-Stack |
| [`03_specialized_mechanics/document_rendering_mechanics.md`](file:///c:/Users/jossl.000/Downloads/Compressed/smk-doc-server/smk-doc-server/docs/AI/03_specialized_mechanics/document_rendering_mechanics.md) | Gotenberg streaming, OpenXML DrawingML DocPr fix, Thai formatting | Full-Stack |
