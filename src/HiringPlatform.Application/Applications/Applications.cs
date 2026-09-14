using HiringPlatform.Application.Common;
using HiringPlatform.Domain.Applications;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Iam;
using HiringPlatform.Domain.Identity;
using HiringPlatform.Domain.Jobs;

namespace HiringPlatform.Application.Applications;

public sealed record StageChangeDto(
    ApplicationStage From,
    ApplicationStage To,
    Guid ChangedBy,
    string? Note,
    DateTimeOffset At
);

public sealed record ApplicationDto(
    Guid Id,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateName,
    string CandidateEmail,
    string CoverLetter,
    string? ResumeUrl,
    ApplicationStage Stage,
    IReadOnlyList<ApplicationStage> NextStages,
    IReadOnlyList<StageChangeDto> History,
    DateTimeOffset AppliedAt
)
{
    private static readonly ApplicationStage[] StaffMoves =
        Enum.GetValues<ApplicationStage>().Where(s => s != ApplicationStage.Withdrawn).ToArray();

    public static ApplicationDto From(JobApplication a, Job? job, User? candidate) => new(
        a.Id.Value, a.JobId.Value, job?.Title ?? "", a.CandidateId.Value, candidate?.FullName ?? "", candidate?.Email.Value ?? "",
        a.CoverLetter, a.ResumeUrl?.ToString(), a.Stage,
        [.. StaffMoves.Where(s => a.Stage.CanTransitionTo(s))],
        [.. a.History.Select(h => new StageChangeDto(h.From, h.To, h.ChangedBy.Value, h.Note, h.At))],
        a.AppliedAt);
}

internal static class ApplicationMapping
{
    public static async Task<List<ApplicationDto>> ToDtos(
        this IEnumerable<JobApplication> apps, IJobRepository jobs, IUserRepository users, CancellationToken ct)
    {
        var list = apps.ToList();
        var jobById = (await jobs.FindMany(list.Select(a => a.JobId).Distinct(), ct)).ToDictionary(j => j.Id);
        var userById = (await users.FindMany(list.Select(a => a.CandidateId).Distinct(), ct)).ToDictionary(u => u.Id);
        return list
            .Select(a => ApplicationDto.From(a, jobById.GetValueOrDefault(a.JobId), userById.GetValueOrDefault(a.CandidateId)))
            .ToList();
    }
}

// ── Apply ───────────────────────────────────────────────────────────────────

public sealed record ApplyToJob(
    UserId ActorId,
    JobId JobId,
    string? CoverLetter,
    string? ResumeUrl
) : ICommand<ApplicationDto>;

public sealed class ApplyToJobHandler(
    AccessGuard guard,
    IJobRepository jobs,
    IApplicationRepository apps,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<ApplyToJob, ApplicationDto>
{
    public async Task<Result<ApplicationDto>> Handle(ApplyToJob r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await jobs.Find(r.JobId, ct) is not { } job)
            return ApplicationError.NotFound("Job not found");
        if (guard.Check(actor.Value!, AccessGuard.Of(job), AccessAction.ApplicationCreate) is { } denied)
            return denied;
        if (!job.Status.AcceptsApplications())
            return ApplicationError.Validation("This job is not accepting applications");
        if (await apps.Exists(job.Id, actor.Value!.Id, ct))
            return ApplicationError.Conflict("You have already applied to this job");

        var app = JobApplication.Submit(ApplicationId.New(), job.Id, job.CompanyId, actor.Value.Id, r.CoverLetter, r.ResumeUrl, clock.UtcNow);
        apps.Add(app);
        await uow.SaveChanges(ct);
        return ApplicationDto.From(app, job, actor.Value);
    }
}

// ── Pipeline moves ──────────────────────────────────────────────────────────

public sealed record AdvanceApplication(
    UserId ActorId,
    ApplicationId ApplicationId,
    ApplicationStage To,
    string? Note
)
    : ICommand<ApplicationDto>;

public sealed class AdvanceApplicationHandler(
    AccessGuard guard,
    IApplicationRepository apps,
    IJobRepository jobs,
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<AdvanceApplication, ApplicationDto>
{
    public async Task<Result<ApplicationDto>> Handle(AdvanceApplication r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await apps.Find(r.ApplicationId, ct) is not { } app)
            return ApplicationError.NotFound("Application not found");
        if (guard.Check(actor.Value!, AccessGuard.Of(app), AccessAction.ApplicationAdvance) is { } denied)
            return denied;

        var moved = app.Advance(r.To, actor.Value!.Id, r.Note, clock.UtcNow);
        apps.Update(moved);
        await uow.SaveChanges(ct);
        return ApplicationDto.From(moved, await jobs.Find(moved.JobId, ct), await users.Find(moved.CandidateId, ct));
    }
}

public sealed record WithdrawApplication(
    UserId ActorId,
    ApplicationId ApplicationId
) : ICommand<ApplicationDto>;

public sealed class WithdrawApplicationHandler(
    AccessGuard guard,
    IApplicationRepository apps,
    IJobRepository jobs,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<WithdrawApplication, ApplicationDto>
{
    public async Task<Result<ApplicationDto>> Handle(WithdrawApplication r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await apps.Find(r.ApplicationId, ct) is not { } app)
            return ApplicationError.NotFound("Application not found");
        if (guard.Check(actor.Value!, AccessGuard.Of(app), AccessAction.ApplicationWithdraw) is { } denied)
            return denied;

        var withdrawn = app.Withdraw(actor.Value!.Id, clock.UtcNow);
        apps.Update(withdrawn);
        await uow.SaveChanges(ct);
        return ApplicationDto.From(withdrawn, await jobs.Find(withdrawn.JobId, ct), actor.Value);
    }
}

// ── Queries ─────────────────────────────────────────────────────────────────

public sealed record GetApplication(
    UserId ActorId,
    ApplicationId ApplicationId
) : IQuery<ApplicationDto>;

public sealed class GetApplicationHandler(
    AccessGuard guard,
    IApplicationRepository apps,
    IJobRepository jobs,
    IUserRepository users
)
    : IHandler<GetApplication, ApplicationDto>
{
    public async Task<Result<ApplicationDto>> Handle(GetApplication r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await apps.Find(r.ApplicationId, ct) is not { } app)
            return ApplicationError.NotFound("Application not found");
        if (guard.Check(actor.Value!, AccessGuard.Of(app), AccessAction.ApplicationRead) is not null)
            return ApplicationError.NotFound("Application not found");

        return ApplicationDto.From(app, await jobs.Find(app.JobId, ct), await users.Find(app.CandidateId, ct));
    }
}

public sealed record ListMyApplications(
    UserId ActorId
) : IQuery<IReadOnlyList<ApplicationDto>>;

public sealed class ListMyApplicationsHandler(
    IApplicationRepository apps,
    IJobRepository jobs,
    IUserRepository users
)
    : IHandler<ListMyApplications, IReadOnlyList<ApplicationDto>>
{
    public async Task<Result<IReadOnlyList<ApplicationDto>>> Handle(ListMyApplications r, CancellationToken ct) =>
        await (await apps.ListByCandidate(r.ActorId, ct)).ToDtos(jobs, users, ct);
}

public sealed record ListJobApplications(
    UserId ActorId,
    JobId JobId
) : IQuery<IReadOnlyList<ApplicationDto>>;

public sealed class ListJobApplicationsHandler(
    AccessGuard guard,
    IApplicationRepository apps,
    IJobRepository jobs,
    IUserRepository users
)
    : IHandler<ListJobApplications, IReadOnlyList<ApplicationDto>>
{
    public async Task<Result<IReadOnlyList<ApplicationDto>>> Handle(ListJobApplications r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await jobs.Find(r.JobId, ct) is not { } job)
            return ApplicationError.NotFound("Job not found");

        // reading the pipeline of a job = application:read on the job's company
        var resource = new ResourceAttributes(ResourceType.Job, job.Id.Value, null, job.CompanyId);
        if (guard.Check(actor.Value!, resource, AccessAction.ApplicationRead) is { } denied)
            return denied;

        return await (await apps.ListByJob(job.Id, ct)).ToDtos(jobs, users, ct);
    }
}
