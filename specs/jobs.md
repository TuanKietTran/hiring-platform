# Jobs And Public Discovery

Last updated: main@4ca0f29 | 2026-09-15

## Scope

This spec covers `Domain/Jobs/Job.cs`, `Application/Jobs/Jobs.cs`, job endpoint modules, applicant/recruiter service route ownership, gateway job dispatch, job repository behavior, and Angular jobs, job-detail, and staff-dashboard surfaces.

## Business Requirements

- Company staff may create jobs only for their own company.
- New jobs begin as drafts and cannot accept applications until published.
- Public discovery exposes only open jobs.
- Draft, paused, and closed job existence must be hidden from anonymous users and unrelated users.
- Company staff may list and manage all jobs belonging to their own company.
- Job status changes must follow the lifecycle and closed jobs are immutable.
- Search must support text, workplace filtering, bounded pagination, and newest-first presentation.

## Create And Edit Flow

```mermaid
flowchart TD
    C[POST /api/jobs] --> Actor[Authenticate and load actor]
    Actor --> Staff{Has company staff identity?}
    Staff -- no --> Deny[403]
    Staff -- yes --> Validate[Validate title, description, salary, skills]
    Validate --> ABAC[job:write on actor company]
    ABAC -->|deny| Deny
    ABAC -->|allow| Draft[Create draft and persist]

    U[PUT /api/jobs/id] --> Load[Load job]
    Load -->|missing| NotFound[404]
    Load --> Own[job:write on job company]
    Own -->|deny| Deny
    Own --> Closed{Closed?}
    Closed -- yes --> Invalid[Validation error]
    Closed -- no --> Replace[Replace editable details and persist]
```

Title is required and limited to 150 characters; description is limited to 20,000. Location is trimmed. Skills are normalized. Salary uses minor units, requires one supported currency, and requires minimum not greater than maximum. Employment values are full-time, part-time, contract, or internship; workplace values are on-site, hybrid, or remote.

## Status Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Open: publish
    Draft --> Closed: close
    Open --> Paused: pause
    Open --> Closed: close
    Paused --> Open: publish
    Paused --> Closed: close
    Closed --> [*]
```

`POST /api/jobs/{id}/status` requires `job:publish` for same-company staff. The first publication sets `PublishedAt`; republishing a paused job preserves it. Closing sets `ClosedAt`. Open alone means public and accepts applications.

## Search And Detail Flow

```mermaid
flowchart TD
    Search[GET /api/jobs] --> Open[Filter status=open]
    Open --> Text{Search text supplied?}
    Text -- yes --> ILike[ILIKE indexed row SearchText content]
    Text -- no --> Workplace
    ILike --> Workplace{Workplace filter?}
    Workplace -- yes --> Deserialize[Deserialize and filter workplace in memory]
    Workplace -- no --> Page
    Deserialize --> Page[Clamp page >=1 and size 1..100]
    Page --> Results[Return items, total, page, pageSize]

    Detail[GET /api/jobs/id] --> Found{Job exists?}
    Found -- no --> NF[404]
    Found -- yes --> Public{Open?}
    Public -- yes --> Return[Return job]
    Public -- no --> Signed{Authenticated same-company staff allowed?}
    Signed -- no --> NF
    Signed -- yes --> Return
```

Search text covers title, description, location, and skills through the denormalized `SearchText` column. Results are ordered by descending version-7-like identifier rather than an explicit publication timestamp. Company names are joined through repository lookups when mapping DTOs.

Applicant service owns public GET job routes; recruiter service owns company listing and mutations. The gateway distinguishes public detail from `/applications` suffixes with method/path rules.

## User Interface Flow

```mermaid
sequenceDiagram
    actor Visitor
    participant Jobs as /jobs
    participant Detail as /jobs/:id
    participant API

    Visitor->>Jobs: Search text/workplace
    Jobs->>API: GET jobs?page=1&pageSize=50
    API-->>Jobs: Open roles
    Visitor->>Detail: Select role
    Detail->>API: GET job
    API-->>Detail: Public details or 404
```

The staff dashboard creates drafts and offers publish/pause/close actions according to displayed status. The API remains authoritative if UI state is stale.

## Current Gaps

- The Angular client has no job-edit form and does not collect salary when creating a role.
- Workplace filtering and pagination happen after loading/deserializing all matching open rows, limiting scale.
- Text search uses a broad `ILIKE` expression with no PostgreSQL full-text/trigram index.
- Repository ordering by ID assumes ID chronology and does not explicitly order by publication date.
- There is no job deletion, reopening after close, expiry, approval workflow, department, timezone, or application deadline.
- Concurrent edits/status changes have no row version and can overwrite one another.
