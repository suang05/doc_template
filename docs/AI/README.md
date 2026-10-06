# AI Documentation Hub (`docs/AI/`) — SMK Document Server

> **Authoritative Navigation Index for AI Coding Agents**  
> System: `smk-doc-server` (.NET 10 / Next.js 15 / Gotenberg 8 / PostgreSQL 15 / MinIO)  
> Updated September 2026.

Welcome, AI Agent. This directory contains the complete modular documentation for **smk-doc-server**.  
Do **NOT** load all documents into your context simultaneously. Use **Progressive Disclosure** by consulting this routing table to identify and load only the file relevant to your current task scope.

---

## 🗺️ Master Documentation Routing Table

| If your task involves... | Consult this document | Key Contents & Responsibilities |
|---|---|---|
| **Coding Standards & Syntax** | [CODING_CONVENTIONS.md](CODING_CONVENTIONS.md) | C# 13 / TypeScript idioms, Naming Matrix, Async/CancellationToken, Error Handling |
| **System Architecture & Boundaries** | [ARCHITECTURE.md](ARCHITECTURE.md) | 4 Clean Architecture layers, M2M vs Portal Auth channels, Gotenberg/MinIO pipeline |
| **Directory Map & File Catalog** | [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) | Full folder tree, complete list of Entities, Enums, Value Objects, UseCases |
| **Design Patterns (Canonical)** | [PATTERNS.md](PATTERNS.md) | Thin Controllers, Sealed UseCases, Strategy Pattern, Positional Records, FluentValidation |
| **Prohibited Patterns & Anti-Patterns** | [ANTI-PATTERNS.md](ANTI-PATTERNS.md) | AP-001 to AP-041: What NOT to do, with explicit ❌/✅ code comparison diffs |
| **Database Schema & Migrations** | [DB_SCHEMA.md](DB_SCHEMA.md) | PostgreSQL tables, columns, UUIDv7 PKs, indexes, cascade delete rules |
| **REST Endpoints & DTO Contracts** | [API_CONTRACT.md](API_CONTRACT.md) | OpenAPI/REST specs, Request/Response payloads, RFC 7807 Error codes, API Keys |
| **Template Engine & PDF Generation** | [TEMPLATE_ENGINE.md](TEMPLATE_ENGINE.md) | Handlebars helpers, Chromium vs LibreOffice, Thai font sync, Baht text, Word/Excel engines |
| **Frontend Portal UI & Styling** | [DESIGN.md](DESIGN.md) | Next.js 15 App Router, Monaco Editor, Tailwind design tokens, HyperUI patterns |
| **Architectural Decisions (ADRs)** | [DECISIONS.md](DECISIONS.md) | Historic ADRs, technology selections, trade-off rationale |
| **Local Setup & Ports** | [ONBOARDING.md](ONBOARDING.md) | Docker Compose services, local ports, database seeds, troubleshooting |

---

## 🏛️ The 4 Documentation Pillars

```
docs/AI/
├── 🏛️ 1. Architecture & Blueprint
│   ├── ARCHITECTURE.md          # System topology & Clean Architecture layer boundaries
│   ├── PROJECT_STRUCTURE.md     # Comprehensive file map & domain model catalog
│   └── DB_SCHEMA.md             # PostgreSQL relational schema & indexes
│
├── 💎 2. Engineering Standards & Quality
│   ├── CODING_CONVENTIONS.md    # C# 13 & TS language idioms, naming rules, async safety
│   ├── PATTERNS.md              # Canonical architectural patterns & best practices
│   └── ANTI-PATTERNS.md         # Explicit catalog of prohibited code patterns
│
├── 🔌 3. Contracts & Domain Engines
│   ├── API_CONTRACT.md          # REST API specs, envelopes, and M2M authentication
│   ├── TEMPLATE_ENGINE.md       # Handlebars, Gotenberg Chromium, and Thai typography
│   └── DESIGN.md                # Next.js App Router UI, tokens, and component guidelines
│
└── 📚 4. Project Memory & Operations
    ├── DECISIONS.md             # ADRs and foundational design rationale
    └── ONBOARDING.md            # Environment initialization and container topology
```

---

## ⚡ Core Invariants Quick Reference (Zero-Tolerance Rules)

1. **Stateless Previews:** Never write to PostgreSQL or MinIO during Preview endpoints. Previews must remain 100% in-memory and ephemeral.
2. **HTML-First & Thai Typography:** Chromium is our first-class engine. Always inject Sarabun fonts and Thai word-breaking. Route all Thai date/baht formatting through `ThaiDataTransformer`.
3. **Clean Architecture Inversion:**
   - `Domain`: Pure C# POCOs only. No DTOs, no EF Core, no HTTP models.
   - `Application`: Depends on Domain only. UseCases return Application DTOs only; never expose Domain Entities.
   - `Api`: Controllers translate HTTP ↔ Application DTOs. Never access Domain Entities directly.
4. **Host Safety:** Never execute `docker` or `docker compose` commands autonomously.
