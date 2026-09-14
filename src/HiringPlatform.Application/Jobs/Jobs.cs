using HiringPlatform.Application.Common;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Companies;
using HiringPlatform.Domain.Iam;
using HiringPlatform.Domain.Jobs;

namespace HiringPlatform.Application.Jobs;

public sealed record SalaryDto(
    long MinMinor,
    long MaxMinor,
    Currency Currency
);

public sealed record JobInput(
    string Title,
    string Description,
    string Location,
    EmploymentType EmploymentType,
    WorkplaceType WorkplaceType,
    SalaryDto? Salary,
    string[]? Skills
)
{
    public JobDetails ToDetails() => new(
        Title, Description, Location, EmploymentType, WorkplaceType,
        Salary is null ? null : SalaryRange.Of(Money.Of(Salary.MinMinor, Salary.Currency), Money.Of(Salary.MaxMinor, Salary.Currency)),
        Skills);
}

public sealed record JobDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Title,
    string Description,
    string Location,
    EmploymentType EmploymentType,
    WorkplaceType WorkplaceType,
    SalaryDto? Salary,
    IReadOnlyList<string> Skills,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt
)
{
    public static JobDto From(Job j, Company? c) => new(
        j.Id.Value, j.CompanyId.Value, c?.Name ?? "", j.Title, j.Description, j.Location, j.EmploymentType, j.WorkplaceType,
        j.Salary is { } s ? new SalaryDto(s.Min.AmountMinor, s.Max.AmountMinor, s.Min.Currency) : null,
        j.Skills, j.Status, j.CreatedAt, j.PublishedAt);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize
);

internal static class JobMapping
{
    public static async Task<List<JobDto>> ToDtos(this IEnumerable<Job> jobs, ICompanyRepository companies, CancellationToken ct)
    {
        var list = jobs.ToList();
        var byId = (await companies.FindMany(list.Select(j => j.CompanyId).Distinct(), ct)).ToDictionary(c => c.Id);
        return list.Select(j => JobDto.From(j, byId.GetValueOrDefault(j.CompanyId))).ToList();
    }
}

// ── Create / edit ───────────────────────────────────────────────────────────

public sealed record CreateJob(
    UserId ActorId,
    JobInput Input
) : ICommand<JobDto>;

public sealed class CreateJobHandler(
    AccessGuard guard,
    IJobRepository jobs,
    ICompanyRepository companies,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<CreateJob, JobDto>
{
    public async Task<Result<JobDto>> Handle(CreateJob r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (actor.Value!.CompanyId is not { } companyId)
            return ApplicationError.Forbidden("Only company staff can post jobs");

        var job = Job.Draft(JobId.New(), companyId, actor.Value.Id, r.Input.ToDetails(), clock.UtcNow);
        if (guard.Check(actor.Value, AccessGuard.Of(job), AccessAction.JobWrite) is { } denied)
            return denied;

        jobs.Add(job);
        await uow.SaveChanges(ct);
        return JobDto.From(job, await companies.Find(companyId, ct));
    }
}

public sealed record UpdateJob(
    UserId ActorId,
    JobId JobId,
    JobInput Input
) : ICommand<JobDto>;

public sealed class UpdateJobHandler(
    AccessGuard guard,
    IJobRepository jobs,
    ICompanyRepository companies,
    IUnitOfWork uow
)
    : IHandler<UpdateJob, JobDto>
{
    public async Task<Result<JobDto>> Handle(UpdateJob r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await jobs.Find(r.JobId, ct) is not { } job)
            return ApplicationError.NotFound("Job not found");
        if (guard.Check(actor.Value!, AccessGuard.Of(job), AccessAction.JobWrite) is { } denied)
            return denied;

        var edited = job.Edit(r.Input.ToDetails());
        jobs.Update(edited);
        await uow.SaveChanges(ct);
        return JobDto.From(edited, await companies.Find(edited.CompanyId, ct));
    }
}

// ── Status changes ──────────────────────────────────────────────────────────

public enum JobStatusChange
{
    Publish, Pause, Close
}

public sealed record ChangeJobStatus(
    UserId ActorId,
    JobId JobId,
    JobStatusChange Change
) : ICommand<JobDto>;

public sealed class ChangeJobStatusHandler(
    AccessGuard guard,
    IJobRepository jobs,
    ICompanyRepository companies,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<ChangeJobStatus, JobDto>
{
    public async Task<Result<JobDto>> Handle(ChangeJobStatus r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await jobs.Find(r.JobId, ct) is not { } job)
            return ApplicationError.NotFound("Job not found");
        if (guard.Check(actor.Value!, AccessGuard.Of(job), AccessAction.JobPublish) is { } denied)
            return denied;

        var now = clock.UtcNow;
        var changed = r.Change switch
        {
            JobStatusChange.Publish => job.Publish(now),
            JobStatusChange.Pause => job.Pause(),
            JobStatusChange.Close => job.Close(now),
            _ => throw new DomainException($"unknown status change: {r.Change}"),
        };

        jobs.Update(changed);
        await uow.SaveChanges(ct);
        return JobDto.From(changed, await companies.Find(changed.CompanyId, ct));
    }
}

// ── Queries ─────────────────────────────────────────────────────────────────

public sealed record SearchJobs(
    string? Text,
    WorkplaceType? Workplace,
    int Page = 1,
    int PageSize = 20
) : IQuery<PagedResult<JobDto>>;

public sealed class SearchJobsHandler(
    IJobRepository jobs,
    ICompanyRepository companies
) : IHandler<SearchJobs, PagedResult<JobDto>>
{
    public async Task<Result<PagedResult<JobDto>>> Handle(SearchJobs r, CancellationToken ct)
    {
        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 100);
        var (items, total) = await jobs.SearchOpen(new JobSearch(r.Text?.Trim(), r.Workplace, page, size), ct);
        return new PagedResult<JobDto>(await items.ToDtos(companies, ct), total, page, size);
    }
}

/// <summary>Anonymous callers pass no actor and can only see open jobs.</summary>
public sealed record GetJob(
    UserId? ActorId,
    JobId JobId
) : IQuery<JobDto>;

public sealed class GetJobHandler(
    AccessGuard guard,
    IJobRepository jobs,
    ICompanyRepository companies
) : IHandler<GetJob, JobDto>
{
    public async Task<Result<JobDto>> Handle(GetJob r, CancellationToken ct)
    {
        if (await jobs.Find(r.JobId, ct) is not { } job)
            return ApplicationError.NotFound("Job not found");

        if (!job.IsPublic)
        {
            // hide existence of unpublished jobs from outsiders
            if (r.ActorId is not { } actorId)
                return ApplicationError.NotFound("Job not found");
            var actor = await guard.Actor(actorId, ct);
            if (!actor.IsSuccess)
                return actor.Error!;
            if (guard.Check(actor.Value!, AccessGuard.Of(job), AccessAction.JobRead) is not null)
                return ApplicationError.NotFound("Job not found");
        }

        return JobDto.From(job, await companies.Find(job.CompanyId, ct));
    }
}

public sealed record ListCompanyJobs(
    UserId ActorId
) : IQuery<IReadOnlyList<JobDto>>;

public sealed class ListCompanyJobsHandler(
    AccessGuard guard,
    IJobRepository jobs,
    ICompanyRepository companies
)
    : IHandler<ListCompanyJobs, IReadOnlyList<JobDto>>
{
    public async Task<Result<IReadOnlyList<JobDto>>> Handle(ListCompanyJobs r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (actor.Value!.CompanyId is not { } companyId)
            return ApplicationError.Forbidden("Only company staff can list company jobs");

        return await (await jobs.ListByCompany(companyId, ct)).ToDtos(companies, ct);
    }
}
