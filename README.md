# Hirelane — Hiring Platform

A domain-driven hiring platform built with **.NET 10 / Aspire 13** and **Angular 22 (strict TypeScript)**. It carries the architectural foundations from `cv-sv` into hiring: immutable domain transitions, CQRS handlers, repository ports, ABAC with deny-overrides, validated value objects, and adapter isolation.

## Product flows

- Candidates register, discover open jobs, apply once, track progress, and withdraw.
- Companies register an initial org admin, add hiring staff, draft/publish/pause/close jobs, and manage candidate pipelines.
- Application transitions are enforced: `applied → screening → interviewing → offered → hired`, with terminal reject/withdraw paths.
- Interview scheduling validates company membership, stage, duration, and panel conflicts.
- ABAC enforces owner/company boundaries in application handlers, independently of the Angular UI.

## Architecture

```text
Angular TypeScript SPA
        ↓ HTTP
HiringPlatform.Api             endpoints, cookie auth
        ↓ Mediator
HiringPlatform.Application     CQRS handlers + repository ports
        ↓
HiringPlatform.Domain          pure aggregates, value objects, ABAC
        ↑
HiringPlatform.Infrastructure  EF Core/PostgreSQL adapters

HiringPlatform.AppHost         Aspire orchestration + telemetry/health
```

Dependencies point inward. `Domain` has no framework dependency. PostgreSQL rows keep searchable/indexed columns while aggregate snapshots remain domain-owned.

## Run in the restart-safe OrbStack dev pod

Prerequisite: open OrbStack with Kubernetes enabled.

```bash
cd /Users/handlerone/Downloads/hiring-platform
./dev.sh start
./dev.sh ports
```

Then open:

- Angular app: http://localhost:4201
- Aspire dashboard: http://localhost:15888 (the tokenized login URL is in `./dev.sh logs`)

After restarting the Mac, open OrbStack and run those same two commands. Kubernetes recreates both Deployments; PVCs retain PostgreSQL data, Linux `node_modules`, and NuGet caches, while the host-mounted source remains on macOS.

Useful commands:

```bash
./dev.sh up                 # ensure pod exists
./dev.sh start              # idempotently start full stack
./dev.sh stop
./dev.sh logs
./dev.sh shell
./dev.sh exec 'dotnet test HiringPlatform.slnx'
./dev.sh rebuild            # after changing .devcontainer/Dockerfile
```

## Validation

```bash
./dev.sh exec 'cd /workspace && dotnet build HiringPlatform.slnx'
./dev.sh exec 'cd /workspace && dotnet test HiringPlatform.slnx'
./dev.sh exec 'cd /workspace/src/hiring-web && npm run build && npm test'
```

## Prototype boundaries

- Schema startup currently uses `EnsureCreated`; introduce versioned EF migrations before production.
- Cookie authentication is appropriate for this same-origin SPA, but production deployment should add explicit antiforgery enforcement, external secret management, email verification, reset flows, and hardened data-protection key storage.
- Resume handling currently stores an HTTPS URL, not uploaded files.
