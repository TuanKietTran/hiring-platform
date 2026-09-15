# Hirelane — Hiring Platform

A domain-driven hiring platform built with **.NET 10 / Aspire 13** and **Angular 22 (strict TypeScript)**. It carries the architectural foundations from `cv-sv` into hiring: immutable domain transitions, CQRS handlers, repository ports, ABAC with deny-overrides, validated value objects, and adapter isolation.

## Product flows

- Candidates register, maintain a professional profile and versioned HTTPS CV library, discover open jobs, apply once, track progress, and withdraw.
- Companies register an initial org admin, add hiring staff, draft/publish/pause/close jobs, and manage candidate pipelines.
- Application transitions are enforced: `applied → screening → interviewing → offered → hired`, with terminal reject/withdraw paths.
- Interview scheduling validates company membership, stage, duration, and panel conflicts.
- ABAC enforces owner/company boundaries in application handlers, independently of the Angular UI.

## Architecture

```text
Angular TypeScript SPA
        ↓ same-origin HTTP
HiringPlatform.Gateway                 edge routing + load balancing
        ├── IdentityService × 2        users, staff identity, authentication, ABAC/PDP
        ├── ApplicantService × 2       job discovery, applications, profile + CV library
        └── RecruiterService × 2       roles, candidate pipelines, interviews
                         ↓ Mediator
HiringPlatform.Application             CQRS handlers + repository ports
                         ↓
HiringPlatform.Domain                  pure aggregates, value objects, deny-overrides ABAC
                         ↑
HiringPlatform.Infrastructure          EF Core/PostgreSQL adapters

Hirelane.ServiceClients                public typed REST client package for service-to-service calls
HiringPlatform.AppHost                 Aspire orchestration, discovery, replicas, telemetry/health
```

The gateway is the only browser API and keeps the existing `/api` contract. Aspire service discovery distributes calls over two healthy replicas of each service. Authentication cookies use one application name and a shared protected-key directory, so identity-issued sessions are accepted by the applicant and recruiter services.

Internal callers use the interfaces exported by `src/HiringPlatform.ServiceClients` rather than constructing service URLs. That package owns public route constants and REST contracts (`IIdentityServiceClient`, `IApplicantServiceClient`, and `IRecruiterServiceClient`). The identity service exposes `/api/access/evaluate` as the central policy-decision endpoint.

Dependencies still point inward and `Domain` has no framework dependency. The first service extraction deliberately shares the existing PostgreSQL adapter so behavior remains transactional while boundaries settle; splitting schemas/databases and replacing local identity reads with the exported identity client are the next extraction steps.

## Run in the restart-safe OrbStack dev pod

Prerequisite: open OrbStack with Kubernetes enabled.

```bash
cd /Users/handlerone/Downloads/hiring-platform
./dev.sh start
./dev.sh ports
```

Then open:

- Angular app: http://localhost:4201
- Aspire dashboard: use the tokenized `https://localhost:15888/login?t=…` URL printed by `./dev.sh ports`

Development startup idempotently seeds these local-only accounts:

| Role | Email | Password |
| --- | --- | --- |
| Candidate | `candidate@hirelane.dev` | `HirelaneDev1!` |
| Organization admin | `admin@hirelane.dev` | `HirelaneDev1!` |

The one-shot migrator hashes these passwords through the production password hasher. Seeding requires both the `Development` environment and the local launch profile's explicit `DevelopmentSeed:Enabled` opt-in; it never overwrites existing accounts.

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
- CV handling currently stores versioned HTTPS URLs, not uploaded binaries.
- The extracted services currently share one database as a transitional deployment boundary; move to service-owned schemas and asynchronous integration events before independent scaling in production.
