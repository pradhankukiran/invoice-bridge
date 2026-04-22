<p align="center">
  <img src="./docs/logo.svg" width="180" alt="InvoiceBridge"/>
</p>

<h1 align="center">InvoiceBridge</h1>

<p align="center">
  <strong>Production-grade accounts payable automation for .NET</strong><br/>
  <sub>UBL invoice ingestion · 3-way match · approval workflow · accounting export — built on Clean Architecture, Blazor Server, and PostgreSQL.</sub>
</p>

<p align="center">
  <a href="https://github.com/pradhankukiran/invoice-bridge/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/pradhankukiran/invoice-bridge/ci.yml?branch=main&style=flat-square&label=CI&logo=github" alt="CI"/></a>
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=c-sharp&logoColor=white" alt="C# 12"/>
  <img src="https://img.shields.io/badge/Blazor-Server-512BD4?style=flat-square&logo=blazor&logoColor=white" alt="Blazor Server"/>
  <img src="https://img.shields.io/badge/EF_Core-8.0-512BD4?style=flat-square&logo=nuget&logoColor=white" alt="EF Core 8"/>
  <img src="https://img.shields.io/badge/PostgreSQL-16-4169E1?style=flat-square&logo=postgresql&logoColor=white" alt="PostgreSQL 16"/>
  <img src="https://img.shields.io/badge/SQLite-dev/test-003B57?style=flat-square&logo=sqlite&logoColor=white" alt="SQLite"/>
  <img src="https://img.shields.io/badge/Docker-ready-2496ED?style=flat-square&logo=docker&logoColor=white" alt="Docker"/>
  <img src="https://img.shields.io/badge/OpenTelemetry-1.9-425CC7?style=flat-square&logo=opentelemetry&logoColor=white" alt="OpenTelemetry"/>
  <img src="https://img.shields.io/badge/Serilog-8.0-0D4C75?style=flat-square" alt="Serilog"/>
  <img src="https://img.shields.io/badge/FluentValidation-11-00B5BE?style=flat-square" alt="FluentValidation"/>
  <img src="https://img.shields.io/badge/MailKit-4.16-0078D4?style=flat-square" alt="MailKit"/>
  <img src="https://img.shields.io/badge/xUnit-2.9-5A29E4?style=flat-square" alt="xUnit"/>
  <img src="https://img.shields.io/badge/UBL-2.1-1F6FEB?style=flat-square" alt="UBL 2.1"/>
  <img src="https://img.shields.io/badge/clean--architecture-enforced-0A2540?style=flat-square" alt="Clean Architecture"/>
  <img src="https://img.shields.io/github/last-commit/pradhankukiran/invoice-bridge?style=flat-square" alt="Last commit"/>
  <img src="https://img.shields.io/github/repo-size/pradhankukiran/invoice-bridge?style=flat-square" alt="Repo size"/>
</p>

---

## Table of contents

- [Overview](#overview)
- [Highlights](#highlights)
- [Architecture](#architecture)
- [Invoice lifecycle](#invoice-lifecycle)
- [Stack](#stack)
- [Quickstart](#quickstart)
- [Project layout](#project-layout)
- [Configuration reference](#configuration-reference)
- [Observability](#observability)
- [Security posture](#security-posture)
- [Testing](#testing)
- [Continuous integration](#continuous-integration)
- [Roadmap](#roadmap)

## Overview

InvoiceBridge is a back-office application for **supplier-invoice automation**. It ingests UBL 2.1 XML invoices, reconciles them against purchase orders and goods receipts, routes the result through a role-based approval workflow, and emits accounting exports downstream.

It is deliberately built as a *production scaffold*, not a demo:

- Clean Architecture separation (Domain · Application · Infrastructure · Web)
- EF Core **migrations** on PostgreSQL — no `EnsureCreated`, no drop-on-drift
- **Optimistic concurrency** on every mutating aggregate
- **Background workers** driving the import queue, approval SLA, and notification outbox
- **Transactional outbox** pattern around notification delivery
- **Structured logging** (Serilog JSON), **OpenTelemetry** traces + metrics, split **liveness/readiness** probes
- **Rate limiting**, CSP + security headers, XXE-safe XML parser, RFC 7807 `ProblemDetails`
- **FluentValidation** on mutation DTOs, `ValidateOnStart` on options
- Non-root **multi-stage Docker image**, `docker-compose` stack with Postgres + MailHog + OTel Collector
- **GitHub Actions** CI with vulnerability scan

## Highlights

| Capability | What it does |
| --- | --- |
| **UBL 2.1 invoice ingestion** | Streaming XML parser with optional schema validation. DTD processing disabled and `XmlResolver` nulled out — XXE-safe by construction. |
| **3-way match engine** | Reconciles Invoice ↔ Purchase Order ↔ Goods Receipt line by line with configurable quantity / price / tax variance rules. |
| **Supplier mapping profiles** | Per-supplier item-code and tax-rate mapping with optional *require-mapped* strictness. |
| **Approval workflow** | Role-based routing with SLA evaluator; escalations and breaches are published to the right inbox automatically. |
| **Accounting export** | Pluggable CSV/XML export pipeline with downloadable artefacts and per-invoice status transitions. |
| **Payments ledger** | Records partial and full payments with running outstanding balance; state machine safeguards double-payment. |
| **Audit trail** | Every mutation writes an `AuditLog` row enriched with actor, IP, user agent, and request correlation id. |
| **Retryable import queue** | Queue + retry + next-retry-at scheduling driven by a `BackgroundService` worker — not an operator-triggered button. |
| **Notification outbox** | Notification intent is persisted inside the same transaction as the business write; a worker dispatches via SMTP with exponential backoff. |

## Architecture

```mermaid
flowchart LR
    U["User Browser"] --> BS
    U --> API

    subgraph Web["InvoiceBridge.Web"]
        BS["Blazor Server UI"]
        API["Minimal APIs\n/auth/* · /api/exports/*"]
        IQW["ImportQueueWorker"]
        SLAW["ApprovalSlaWorker"]
        NOW["NotificationOutboxWorker"]
    end

    subgraph App["InvoiceBridge.Application"]
        SVC["Domain Services\nInvoice · Approval · Export · Payment"]
        VAL["FluentValidation"]
        DISP["NotificationOutboxDispatcher"]
    end

    subgraph Infra["InvoiceBridge.Infrastructure"]
        EF["EF Core 8\n+ RowVersionInterceptor\n+ AuditEnrichmentInterceptor"]
    end

    subgraph Runtime["Runtime dependencies"]
        PG[("PostgreSQL 16")]
        SMTP["SMTP / MailHog"]
        OTEL["OTel Collector"]
    end

    BS --> SVC
    API --> SVC
    IQW --> SVC
    SLAW --> SVC
    NOW --> DISP
    SVC --> VAL
    SVC --> EF
    DISP --> EF
    DISP --> SMTP
    EF --> PG
    Web --> OTEL
    App --> OTEL
```

## Invoice lifecycle

```mermaid
sequenceDiagram
    autonumber
    participant User
    participant Web as InvoiceBridge.Web
    participant Queue as ImportQueueWorker
    participant Svc as InvoiceService
    participant DB as PostgreSQL
    participant Outbox as NotificationOutboxWorker
    participant SMTP

    User->>Web: Upload UBL XML
    Web->>Svc: QueueImportAsync(request)
    Svc->>DB: FileImport (Queued)
    Web-->>User: 202 Accepted

    loop every 15s
        Queue->>Svc: ProcessImportQueueAsync(batch)
    end
    Svc->>Svc: Validate + parse UBL
    Svc->>Svc: Apply supplier mapping
    Svc->>DB: Invoice + InvoiceLines
    Svc->>Svc: Run 3-way match

    alt match succeeds
        Svc->>DB: ApprovalRequest (Pending)
        Svc->>DB: UserNotification + Outbox row (single txn)
    else variance detected
        Svc->>DB: Invoice.Status = Exception
    end

    loop every 10s
        Outbox->>SMTP: Send digest (retry w/ backoff)
    end
```

## Stack

| Layer | Technology |
| --- | --- |
| **Presentation** | Blazor Server (interactive-server render) · Minimal APIs · Cookie auth |
| **Domain / Application** | Clean Architecture with `IApplicationDbContext` abstraction · FluentValidation · xUnit integration tests |
| **Persistence** | EF Core 8 · **PostgreSQL 16** (prod) · **SQLite** (dev / tests) · optimistic concurrency via row-version interceptor · EF Core migrations |
| **Async / workflow** | `IHostedService` workers · transactional notification outbox · retry with exponential backoff · approval SLA evaluator |
| **Logging** | Serilog (console + rolling JSON file) with request enrichment |
| **Telemetry** | OpenTelemetry traces + metrics — ASP.NET Core, HttpClient, EF Core, Runtime — over OTLP |
| **Mail** | MailKit 4.16 SMTP with options validation |
| **Resilience** | Kestrel body-size cap · `Microsoft.Extensions.Http.Resilience` default pipeline · EF `EnableRetryOnFailure` |
| **Security** | Cookie authentication · role-based policies · rate limiting on auth + downloads · CSP, X-Frame-Options, Referrer-Policy, COOP / CORP · XXE-safe XML parser |
| **Container / ops** | Multi-stage Debian-slim Docker image · non-root runtime user · `docker-compose` with Postgres, MailHog, OTel Collector |
| **CI** | GitHub Actions — build · test · vulnerability scan · Docker Buildx |

## Quickstart

### Full stack with Docker Compose

```bash
docker compose up -d
```

| Endpoint | URL |
| --- | --- |
| Application | http://localhost:8080 |
| MailHog (captured email) | http://localhost:8025 |
| Postgres | `postgresql://invoicebridge:invoicebridge@localhost:5432/invoicebridge` |
| OTLP gRPC / HTTP | `localhost:4317` / `localhost:4318` |

The compose stack seeds sample suppliers, products, and a mapping profile on first boot (`Persistence__SeedOnStartup=true`).

### Local development (SQLite, no containers)

```bash
dotnet run --project InvoiceBridge.Web
```

`appsettings.Development.json` defaults to SQLite in `invoicebridge.dev.db` and enables on-startup seeding.

### Demo credentials

All users share `Pass@123`. Roles are configurable under `Auth:Users` in `appsettings.json`.

| User | Roles |
| --- | --- |
| `integration.admin` | Admin, IntegrationAdmin, AP, Manager, Finance, Compliance, Procurement, Warehouse |
| `ap.accountant` | AP |
| `manager.approver` | Manager |
| `finance.officer` | Finance |
| `procurement.officer` | Procurement |
| `warehouse.receiver` | Warehouse |
| `compliance.auditor` | Compliance |

> Replace the `DemoUserStore` with ASP.NET Core Identity or an external IdP before going to production — the demo store validates plaintext passwords from config.

### EF Core migrations

```bash
dotnet ef migrations add MyMigration \
  --project InvoiceBridge.Infrastructure \
  --startup-project InvoiceBridge.Infrastructure \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project InvoiceBridge.Infrastructure \
  --startup-project InvoiceBridge.Infrastructure
```

Set `INVOICEBRIDGE_DESIGNTIME_CONNECTION` to override the design-time Postgres connection string used by the `DesignTimeDbContextFactory`.

## Project layout

```
Invoice-Bridge/
├── InvoiceBridge.Domain/           # Entities + enums (no dependencies)
│   └── Entities/
├── InvoiceBridge.Application/      # Services, DTOs, validators, workflow
│   ├── Abstractions/               # Interfaces for infra + services
│   ├── Common/                     # AuditTrailWriter, shared primitives
│   ├── DTOs/
│   ├── Matching/                   # 3-way match rule evaluator
│   ├── Services/                   # IInvoiceService, IApprovalService, …
│   ├── Validation/                 # FluentValidation rules
│   └── Workflow/                   # SLA evaluator, payment balance, mapping
├── InvoiceBridge.Infrastructure/   # EF Core, migrations, interceptors
│   ├── Persistence/
│   │   ├── Migrations/
│   │   ├── InvoiceBridgeDbContext.cs
│   │   ├── RowVersionInterceptor.cs
│   │   └── AuditEnrichmentInterceptor.cs
│   └── Seed/
├── InvoiceBridge.Web/              # Blazor + APIs + workers + telemetry
│   ├── Components/                 # Razor pages + layout
│   ├── Observability/              # OTel source names
│   ├── Security/                   # Auth, CSP, rate-limit, SMTP, audit accessor
│   ├── Workers/                    # BackgroundService hosted workers
│   └── Program.cs                  # Composition root
├── InvoiceBridge.Tests/            # xUnit integration tests (in-memory SQLite)
├── deploy/                         # OTel Collector config
├── sample-data/                    # UBL 2.1 sample invoices
├── Dockerfile                      # Multi-stage, non-root
├── docker-compose.yml              # Postgres + MailHog + OTel + Web
└── .github/workflows/ci.yml        # Build · test · vuln scan · Docker
```

## Configuration reference

All settings bind from environment variables (double-underscore separator) or `appsettings*.json`.

### Persistence

| Key | Values / Default | Description |
| --- | --- | --- |
| `Persistence__Provider` | `Postgres` (prod) · `Sqlite` (dev) · `SqlServer` | Explicit provider override. Auto-detected from the connection string when unset. |
| `Persistence__SeedOnStartup` | `false` | Runs seeding on boot. Always on in Development. |
| `ConnectionStrings__Postgres` | — | Required when provider is `Postgres`. |
| `ConnectionStrings__SqlServer` | — | Required when provider is `SqlServer`. |
| `ConnectionStrings__Sqlite` | `Data Source=invoicebridge.db` | Dev/test fallback. |

### Workers

| Key | Default | Description |
| --- | --- | --- |
| `Workers__ImportQueue__Enabled` | `true` | Toggle the UBL import worker. |
| `Workers__ImportQueue__PollIntervalSeconds` | `15` | Queue drain cadence. |
| `Workers__ImportQueue__BatchSize` | `10` | Imports per tick. |
| `Workers__ApprovalSla__PollIntervalSeconds` | `60` | SLA evaluation cadence. |
| `Workers__ApprovalSla__WarningThresholdHours` | `24` | Escalation threshold. |
| `Workers__ApprovalSla__BreachThresholdHours` | `48` | Breach threshold. |
| `Workers__NotificationOutbox__PollIntervalSeconds` | `10` | Outbox drain cadence. |
| `Workers__NotificationOutbox__BatchSize` | `25` | Messages per tick. |

### SMTP

| Key | Default | Description |
| --- | --- | --- |
| `Smtp__Enabled` | `false` | Switches the notification sender between MailKit and the logging stub. |
| `Smtp__Host` | — | SMTP host. Required when enabled. |
| `Smtp__Port` | `587` | SMTP port. |
| `Smtp__UseStartTls` | `true` | STARTTLS upgrade. Set to `false` for plaintext MailHog dev. |
| `Smtp__Username` / `Smtp__Password` | — | Optional authentication. |
| `Smtp__FromAddress` | — | Required from address. |
| `Smtp__FromDisplayName` | `InvoiceBridge` | Human-readable sender. |
| `Smtp__TimeoutMilliseconds` | `15000` | Per-message transport timeout. |

### Observability

| Key | Default | Description |
| --- | --- | --- |
| `Otel__OtlpEndpoint` | — | OTLP exporter target. Tracing + metrics remain enabled in-process even when empty; only export is gated. |
| `Serilog__MinimumLevel__Default` | `Information` | Serilog log level. |

## Observability

| Signal | Where it goes |
| --- | --- |
| **Logs** | Serilog — human-readable in Development, Compact JSON elsewhere. Rolling file sink under `logs/` (daily, 64 MB per file, 14-day retention). Each request emits one structured summary with method, path, status, duration, client IP, user, and correlation id. |
| **Traces** | OpenTelemetry — ASP.NET Core, HttpClient, EF Core, plus a shared `InvoiceBridge` `ActivitySource` reserved for business-domain spans. Health requests filtered out. |
| **Metrics** | OpenTelemetry — ASP.NET Core, HttpClient, Runtime (.NET GC, thread pool, exceptions, contention). Custom `InvoiceBridge` meter registered for future business KPIs. |
| **Liveness** | `GET /health/live` — 200 whenever the process can respond. Pair with a K8s liveness probe. |
| **Readiness** | `GET /health/ready` — includes the `AddDbContextCheck` probe. Pair with a K8s readiness probe so traffic is withheld when the DB is unreachable. |
| **Aggregate health** | `GET /health` — every registered probe, for synthetic monitoring dashboards. |

## Security posture

| Concern | Mitigation |
| --- | --- |
| **Brute-force login** | Per-IP fixed window rate limit (10/min) on `/auth/login`. |
| **Request flooding** | Global 200/min limiter + token bucket on export downloads. |
| **Clickjacking** | `X-Frame-Options: DENY` + `frame-ancestors 'none'` CSP directive. |
| **Cross-site scripting** | `Content-Security-Policy` with explicit source allowlist; `X-Content-Type-Options: nosniff`. |
| **MIME sniffing** | `X-Content-Type-Options: nosniff`. |
| **Referer leakage** | `Referrer-Policy: strict-origin-when-cross-origin`. |
| **Browser APIs** | `Permissions-Policy` denies camera, microphone, geolocation, payment. |
| **Cross-origin** | `Cross-Origin-Opener-Policy` and `Cross-Origin-Resource-Policy` pinned to `same-origin`. |
| **Billion laughs / XXE** | XML parser runs with `DtdProcessing.Prohibit` and `XmlResolver = null`. |
| **Large payload DoS** | Kestrel `MaxRequestBodySize = 8 MB` enforced globally. |
| **Lost updates on concurrent edits** | `RowVersion` concurrency tokens on FileImport, Invoice, ApprovalRequest, Payment, AccountingExport. |
| **Information leakage via errors** | RFC 7807 `ProblemDetails`; stack traces + exception types only exposed in Development. |
| **Audit forensics** | AuditLog captures actor, IP, user agent, correlation id on every mutation. |
| **Config drift** | `ValidateDataAnnotations()` + `ValidateOnStart()` on SMTP + worker options — a misconfigured section fails boot loudly. |

## Testing

```bash
dotnet test
```

Sixteen xUnit integration tests cover the end-to-end pipelines:

- **ImportPipelineIntegrationTests** — queue → process → parse → 3-way match → approval notification
- **ApprovalWorkflowIntegrationTests** — pending queue, SLA escalation / breach notification, decision transitions, double-decide guard
- **AccountingExportIntegrationTests** — CSV + XML export generation, status transitions, download artefact
- **MatchRuleEvaluatorTests** — tolerance rules, quantity / price / tax variances
- **SupplierMappingEngineTests** — required-mapping strictness, override resolution
- **WorkflowCalculatorTests** — payment balance + SLA evaluator primitives

Tests run against in-memory SQLite (`:memory:` shared-cache) so the full suite is hermetic and takes under ten seconds.

## Continuous integration

The CI workflow runs on every push and pull request to `main` with three jobs:

| Job | What it does |
| --- | --- |
| **build-test** | Restore with NuGet caching · `dotnet build -warnaserror` · `dotnet test` with code coverage · upload trx + coverage artefact |
| **security-scan** | `dotnet list package --vulnerable --include-transitive` — fails the build on any Critical / High / Moderate advisory |
| **docker-build** | Builds the multi-stage `Dockerfile` end-to-end with Buildx cache — PRs cannot silently break the image |

Workflow file: [`.github/workflows/ci.yml`](./.github/workflows/ci.yml).

## Roadmap

- [ ] ERP connectors behind `IAccountingExportService` — QuickBooks Online, Xero, NetSuite, SAP
- [ ] ACH / SEPA payment initiation gateway (Modern Treasury · Stripe Treasury)
- [ ] OCR pipeline for PDF invoices — Azure Document Intelligence / AWS Textract
- [ ] Supplier self-service portal (W-9 / W-8 collection, bank verification)
- [ ] Multi-currency FX revaluation + historical rate table
- [ ] Multi-tenant isolation (EF global query filters + tenant-aware auth)
- [ ] Azure SignalR Service / Redis backplane for horizontal scale
- [ ] Data retention + GDPR subject-access tooling
- [ ] Replace `DemoUserStore` with ASP.NET Core Identity or OIDC federation
- [ ] k6 / NBomber load-test suite

---

<p align="center">
  <sub>InvoiceBridge · built on Clean Architecture · © MMXXVI</sub>
</p>
