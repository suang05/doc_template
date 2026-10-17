# INDEX.md — Master Documentation Switchboard & Cognitive Router

<cognitive_priming>
> [PURPOSE_SSOT]: Central Master Router, Context Steering Matrix, and Attention Switchboard for the 3-Tier Cognitive Documentation System across `smk-doc-server`.
> [OPERATIONAL_MODES]:
> - `[MODE: CONTEXT_ROUTING]`: Resolve incoming developer/agent intent to the exact authoritative document and section.
> - `[MODE: DRIFT_SYNCHRONIZATION]`: Execute the 4-Point Synchronization Protocol on codebase mutations.
> [BEACONS: Index, Switchboard, Routing, Architecture, Guardrails, Backend, Frontend, ADR, Living SSoT]
</cognitive_priming>

<prior_inversion_map>
| Navigation Context | Defective Prior (Banned) | Canonical Standard (Mandatory Substitution) | Invariant Logic |
|---|---|---|---|
| **Doc Access Flow** | Scanning large unstructured documents | Master switchboard lookup >> Targeted leaf read | Minimizes token usage & KV-cache pollution |
| **Legacy File Lookup** | Querying flat `docs/AI/*.md` files | Navigating 3-tier structure via `docs/AI/INDEX.md` | Single Source of Truth routing |
| **Negative Patterns** | Inline anti-patterns spread across files | Consolidated in `00_guardrails/FIREWALL.md` | Zero KV-cache negative pattern duplication |
| **Architectural Rules**| Implicit assumptions or legacy prose | Deterministic invariants & Minimal Complete ASTs | 100% executable compiler-grade clarity |
| **Doc Sync Protocol** | Sporadic manual updates without order | 4-Point Synchronization (Header >> Matrix >> SSoT >> Sweep) | Zero document drift across stateless sessions |
</prior_inversion_map>

<severity_hierarchy>
- `[TIER-1-FATAL]`: Never edit code that violates `00_guardrails/FIREWALL.md` (P1-P21). Obey all Tier 1 architectural invariants (UUIDv7, non-blocking async, zero-LOH streaming).
- `[TIER-2-INTEGRITY]`: Route all coding tasks through canonical archetypes in `01_backend/blueprints.md` and `02_frontend/screen_recipes.md`.
- `[TIER-3-PERF]`: Consult `03_archival/decisions.md` before attempting to reopen resolved architectural debates (ADR-001 through ADR-029 / ADR-SEC-06).
</severity_hierarchy>

<switchboard_scope>

## 1. 3-Tier Cognitive Topology Overview

```
docs/AI/
├── INDEX.md                         [Master Switchboard & Cognitive Router]
├── 00_guardrails/                   [TIER 0: Core Invariants & Architectural Firewalls]
│   ├── FIREWALL.md                  [Anti-Patterns SSoT: 21 Rules (P1-P5) & Fixes]
│   └── CONVENTIONS.md               [C# 13 / .NET 10 Micro-ASTs & Naming Standards]
├── 01_backend/                      [TIER 1: Backend Domain Blueprints & Infrastructure]
│   ├── system_topology.md           [C4 Architecture, Stream Pipeline, Solution Tree]
│   ├── blueprints.md                [Feature Slices, PEMR Archetypes 1-9, Recipes]
│   ├── api_contract.md              [Wire Rules, RFC 9457 Errors, 38-Endpoint Manifest]
│   ├── persistence.md               [15-Table DDL, UUIDv7, EF Core Value Converters]
│   └── template_engines.md          [Gotenberg, Handlebars, OpenXML, ClosedXML]
├── 02_frontend/                     [TIER 1: Frontend System & Screen Assembly]
│   ├── design_tokens.md             [Ice-White Theme, 2px-4px Radius, 19 Primitives]
│   └── screen_recipes.md            [AppShell, 4 Stitching Recipes, 14 Routes, Archetypes 10-12]
└── 03_archival/                     [TIER 2: Contextual Rationale & Cold Storage]
    └── decisions.md                 [30 Authoritative Architecture Decision Records]
```

---

## 2. High-Salience Attention Steering & Token Routing Matrix

Use this matrix to instantly navigate to the canonical specification matching any task:

| Incoming Intent / Task Scope | Lexical Search Beacons | Authoritative Document | Target Section |
|---|---|---|---|
| **Anti-Patterns & Prohibitions** | Anti-pattern, Banned, Memory leak, Crash, LOH, UoW, Tests | `00_guardrails/FIREWALL.md` | `<firewall_rule>` catalog (P1-P22) |
| **C# Naming, CQRS, Micro-AST** | Primary constructor, camelCase, Record struct | `00_guardrails/CONVENTIONS.md` | Micro-AST Standards & Layer Rules |
| **System Architecture & Pipeline** | Gotenberg resilience, Zero-LOH, Middleware | `01_backend/system_topology.md` | Clean Architecture & Stream Pipeline |
| **Directory Tree & Solution** | Namespaces, Project layout, Slingshot | `01_backend/system_topology.md` | Solution Directory Tree |
| **Adding New UseCase / Feature** | CQRS, Command, Query, PEMR, UseCase | `01_backend/blueprints.md` | Archetypes 1–9 & Production Recipes |
| **Endpoints, DTOs, HTTP API** | Route, Controller, RFC 9457, ApiKey, Auth | `01_backend/api_contract.md` | Archetypes 4–9 & Endpoint Manifest |
| **Database Schema, DDL, Tables** | PostgreSQL, EF Core, UUIDv7, Index, JSONB | `01_backend/persistence.md` | 15-Table Catalog, DDL & Converters |
| **Word/Excel/PDF Rendering** | Gotenberg, Handlebars, OpenXML, DrawingML | `01_backend/template_engines.md` | Rendering Engine Step Pipelines |
| **Colors, Radius, Badges, Buttons** | Ice-White, Sky Blue, 2px, 4px, Lucide, Tokens | `02_frontend/design_tokens.md` | Design Tokens & 19 Primitives |
| **Frontend Screens, Layouts, UI** | AppShell, Stitching Recipe, Wireframe, RSC | `02_frontend/screen_recipes.md` | 4 Master Recipes & 14 Routes |
| **Live Preview, Monaco, Telemetry** | AbortController, Debounce, Monaco Editor | `02_frontend/screen_recipes.md` | Archetype 11 & Live Preview Pipeline |
| **Past Architectural Decisions** | ADR, Rationale, Why Gotenberg, Benchmark | `03_archival/decisions.md` | ADR-001 through ADR-028 / ADR-SEC-06 |

---

## 3. Legacy Migration & Deprecation Mapping

All 11 legacy documentation files in root `docs/AI/` have been refactored into the 3-Tier Cognitive Topology:

| Legacy Root Document | Replacement Canonical Document | Structural Transformation Applied |
|---|---|---|
| `docs/AI/ANTI-PATTERNS.md` | `docs/AI/00_guardrails/FIREWALL.md` | Consolidated 21 firewall rules across 5 phases; pure positive C# ASTs. |
| `docs/AI/CODING_CONVENTIONS.md` | `docs/AI/00_guardrails/CONVENTIONS.md` | High-density C# 13/.NET 10 micro-ASTs; layer boundaries; 15 craftsmanship invariants. |
| `docs/AI/ARCHITECTURE.md` | `docs/AI/01_backend/system_topology.md` | C4 minimal topology; Stream-over-RAM pipeline; Zero-LOH streaming; Polly v8 policies. |
| `docs/AI/PROJECT_STRUCTURE.md` | `docs/AI/01_backend/system_topology.md` | Clean Architecture solution directory tree; layer dependency inversion rules. |
| `docs/AI/PATTERNS.md` | `docs/AI/01_backend/blueprints.md` | 5-file feature slice structure; PEMR formula; 10 generative code archetypes. |
| `docs/AI/API_CONTRACT.md` | `docs/AI/01_backend/api_contract.md` | 6 endpoint archetypes; RFC 9457 ProblemDetails decision matrix; 38-route manifest. |
| `docs/AI/DB_SCHEMA.md` | `docs/AI/01_backend/persistence.md` | 6 design pillars; 15-table catalog; PostgreSQL 16+ DDL; EF Core Value Converters. |
| `docs/AI/TEMPLATE_ENGINE.md` | `docs/AI/01_backend/template_engines.md` | Strategy dispatch; Chromium Gotenberg; OpenXML DrawingML ID invariant; ClosedXML A4 fit. |
| `docs/AI/DESIGN.md` | `docs/AI/02_frontend/design_tokens.md` | Ice-White palette; 0px-4px radius; category accents; 19 atomic design primitives. |
| `docs/AI/FRONTEND_SPEC.md` | `docs/AI/02_frontend/screen_recipes.md` | AppShell topology; Next.js server proxy; 4 stitching recipes; 14-screen inventory; archetypes. |
| `docs/AI/DECISIONS.md` | `docs/AI/03_archival/decisions.md` | 30 ADRs across 8 domains; negative routing cold storage. |

---

## 4. Living Documentation Governance & Synchronization Protocol

Whenever mutations occur in the codebase, execute this 2-tier protocol:

### Tier 0: Central Mutation Triggers

| Signal Token | Source Mutation Event | Target Document & Mandatory Action |
|---|---|---|
| `[SIGNAL_API_MUTATION]` | Controller, Route, Request/Response DTO, or Auth mutation | Update `01_backend/api_contract.md` (Update route manifest, archetypes, openapi.json) |
| `[SIGNAL_DOMAIN_MUTATION]` | Entity, Strongly-Typed ID, Value Object, or Smart Enum | Update `00_guardrails/CONVENTIONS.md` and `01_backend/persistence.md` |
| `[SIGNAL_DECEPTIVE_BUG]` | Runtime trap (LOH, boxing equality, deadlock, crash) | Update `00_guardrails/FIREWALL.md` (Append rule to `<firewall_rule>`) |
| `[SIGNAL_SCHEMA_MIGRATION]`| EF Core migration, table, column, index, constraint | Update `01_backend/persistence.md` (Update Table Catalog, DDL, Converters) |
| `[SIGNAL_STRUCTURE_MUTATION]`| New UseCase, Interface, Service, or Project slice | Update `01_backend/system_topology.md` and `01_backend/blueprints.md` |
| `[SIGNAL_UI_MUTATION]` | Design tokens, color palette, atomic primitive, screen | Update `02_frontend/design_tokens.md` and `02_frontend/screen_recipes.md` |

### Tier 0: Universal 4-Point Synchronization Protocol

When updating any document in `docs/AI/`, execute these 4 positive actions in sequence:
1. `[UPSTREAM_BREADCRUMB_SYNC]`: Update section breadcrumbs or index headers to prevent local attention blindness.
2. `[MATRIX_AND_CATALOG_SYNC]`: Synchronize corresponding row in quick-lookup matrices and manifests.
3. `[BIDIRECTIONAL_SSOT_LINK]`: Ensure cross-references are mutually linked between guardrails and implementation slices.
4. `[CONTRADICTION_SWEEP]`: Scan touched documentation slices with `grep_search` to eliminate legacy or obsolete tokens.

### Living Governance Workflow (The Propose-and-Wait Cycle)
Whenever a codebase mutation triggers any `[SIGNAL_*]`, the agent MUST execute this 3-stage lifecycle:
1. **STAGE 1 [DETECT]:** Identify the matching signal token and single authoritative target file.
2. **STAGE 2 [PROPOSE & HALT]:** In the response handoff, present the proposed micro-diff, explain the architectural impact, and explicitly ask the user for approval. **DO NOT mutate files in `docs/AI/` autonomously.**
3. **STAGE 3 [APPLY]:** Apply the 4-Point Synchronization Protocol to disk ONLY AFTER the user explicitly confirms (e.g., "Proceed", "Update docs", "Yes").

### Authoring Invariants:
- `[COGNITIVE_ALIGNMENT]`: All documentation updates MUST strictly target LLM Cognitive Anatomy (high token density, compiler-grade ASTs, zero conversational prose). Maintain or elevate established canonical standards.
- `[5_SLOT_CHASSIS_PRESERVATION]`: Every document in `docs/AI/` MUST retain the Universal 5-Slot Cognitive Chassis wrapper tags. Never remove, rename, or collapse them:
  * Slot 1: `<cognitive_priming>` - Operational mode, attention budget, lexical search beacons
  * Slot 2: `<prior_inversion_map>` - Prohibited defective prior vs canonical substitution table
  * Slot 3: `<severity_hierarchy>` - TIER-1-FATAL, TIER-2-INTEGRITY, TIER-3-PERF
  * Slot 4: `<{domain}_scope>` - Minimal complete positive ASTs and schemas (zero placeholder comments)
  * Slot 5: `<verification_gateway>` - Active deterministic verification checklist
- Zero decorative emojis. Zero pseudo-cyberpunk or academic metaphors.
- Canonical symbolic dictionary: `=>`, `!`, `::`, `>>`, `&`, `|`, `>`, `<`.
- Minimal node-only Mermaid (<= 15 nodes, <= 2 levels, direct arrows). Indented text hierarchy for trees.
- Micro-diffs over standalone code blocks. Positive canonical code only.
</switchboard_scope>

<verification_gateway>
- [ ] Master Router indexes all 10 leaf specifications across the 3-Tier topology
- [ ] High-salience attention steering matrix maps every core capability to document and section
- [ ] Legacy migration matrix provides 1:1 mapping from old root files to new 3-tier paths
- [ ] Living documentation synchronization protocol explicitly defines mutation triggers and 4-point sync
</verification_gateway>
