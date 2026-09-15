# Hiring Platform Specs DocumentMap

Last updated: main@4ca0f29 | 2026-09-15

This folder is the compact implementation-truth map for humans and coding agents working on `hiring-platform`. Read this file first, then the owning subsystem specs before changing behavior.

The revision above is the repository base commit inspected. These initial specs also describe the uncommitted bounded-service, applicant-profile, gateway, and persistence work present on 2026-09-15. Source remains authoritative if it changes after this snapshot.

Specs describe implemented contracts and source-supported gaps. They are not product marketing, implementation plans, or substitutes for tests and source inspection.

This `_readme.md` is the DocumentMap and is exempt from the subsystem `Scope` and `Current Gaps` requirements.

## Quality Contract

Every subsystem spec must:

- start with `Last updated: <branch>@<short-sha> | YYYY-MM-DD`;
- name concrete source files, routes, stores, and integration points in `Scope`;
- state business requirements separately from implementation details;
- distinguish server-enforced rules from browser presentation;
- graph every material happy path and show important rejection branches;
- identify ownership, authentication, authorization, personal data, credentials, persistence, and concurrency implications;
- describe current behavior, not desired behavior, unless text is explicitly marked as a gap;
- end with `Current Gaps`, containing only source-supported gaps.

## Working Rules

- Read this map and the owning spec before substantial work.
- For cross-cutting changes also read architecture/runtime, identity/security, persistence, and testing as applicable.
- If source and specs disagree, source is ground truth; report drift and update specs only when requested or when intentionally changing a documented contract.
- Every non-index Markdown file in `specs/` must appear exactly once below.
- Never copy production credentials, password hashes, cover letters, CV contents, feedback, or other personal data into specs, logs, or fixtures.

## Subsystem Map

- [architecture-runtime.md](architecture-runtime.md): bounded services, gateway routing, shared code, Aspire topology, dependency direction, and startup.
- [identity-access-security.md](identity-access-security.md): registration, login/session lifecycle, staff provisioning, shared cookie authentication, and ABAC evaluation.
- [applicant-profiles.md](applicant-profiles.md): applicant-owned profile fields and versioned CV-link library.
- [blob-storage.md](blob-storage.md): generic owner-scoped documents/media storage, raw upload, and range streaming through MinIO.
- [jobs.md](jobs.md): job drafting, editing, publication lifecycle, public discovery, and company listings.
- [applications-pipeline.md](applications-pipeline.md): applying, applicant tracking, recruiter pipeline reads, stage transitions, and withdrawal.
- [interviews.md](interviews.md): scheduling, panel validation/conflicts, visibility, cancellation, and feedback.
- [persistence-migration.md](persistence-migration.md): PostgreSQL row/snapshot model, repositories, one-shot migrator, and development seed.
- [frontend.md](frontend.md): Angular routes, auth restoration, candidate and staff journeys, and UI-only behavior.
- [testing-devops.md](testing-devops.md): build/test/lint commands, development pod, current test coverage, and operational gaps.

## Cross-Cutting Update Triggers

- New service, gateway routing rule, replica behavior, shared client, or startup dependency: update architecture/runtime and testing/devops.
- New auth route, role, claim, cookie setting, policy, access action, secret, or personal-data boundary: update identity/access/security and the owning domain spec.
- New aggregate field, command/query, endpoint, transition, validation, or response shape: update its owning domain spec and frontend when consumed there.
- New table, column, index, snapshot, repository operation, migration, or seed behavior: update persistence/migration and the owning domain spec.
- New blob purpose, metadata, object route, S3 behavior, upload/download policy, or binary consumer: update blob storage, architecture/runtime, security, and the consuming domain spec.
- New Angular page, guard, API call, staff/candidate control, or client-side validation: update frontend and the owning domain spec.
- New test suite, CI gate, runtime prerequisite, generated artifact, or deployment command: update testing/devops.
