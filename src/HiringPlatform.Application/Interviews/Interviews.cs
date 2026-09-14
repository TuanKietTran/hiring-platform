using HiringPlatform.Application.Common;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Iam;
using HiringPlatform.Domain.Interviews;

namespace HiringPlatform.Application.Interviews;

public sealed record InterviewerDto(
    Guid Id,
    string FullName
);

public sealed record FeedbackDto(
    Guid By,
    Recommendation Recommendation,
    string Notes,
    DateTimeOffset At
);

public sealed record InterviewDto(
    Guid Id,
    Guid ApplicationId,
    InterviewKind Kind,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    IReadOnlyList<InterviewerDto> Interviewers,
    InterviewStatus Status,
    FeedbackDto? Feedback
);

internal static class InterviewMapping
{
    public static async Task<List<InterviewDto>> ToDtos(this IEnumerable<Interview> interviews, IUserRepository users, CancellationToken ct)
    {
        var list = interviews.ToList();
        var names = (await users.FindMany(list.SelectMany(i => i.Interviewers).Distinct(), ct)).ToDictionary(u => u.Id, u => u.FullName);

        return list.Select(i => new InterviewDto(
            i.Id.Value, i.ApplicationId.Value, i.Kind, i.StartsAt, (int)i.Duration.TotalMinutes,
            [.. i.Interviewers.Select(id => new InterviewerDto(id.Value, names.GetValueOrDefault(id, "")))],
            i.Status,
            i.Feedback is { } f ? new FeedbackDto(f.By.Value, f.Recommendation, f.Notes, f.At) : null)).ToList();
    }
}

// ── Schedule ────────────────────────────────────────────────────────────────

public sealed record ScheduleInterview(
    UserId ActorId,
    ApplicationId ApplicationId,
    InterviewKind Kind,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    Guid[] InterviewerIds
)
    : ICommand<InterviewDto>;

public sealed class ScheduleInterviewHandler(
    AccessGuard guard,
    IApplicationRepository apps,
    IInterviewRepository interviews,
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<ScheduleInterview, InterviewDto>
{
    public async Task<Result<InterviewDto>> Handle(ScheduleInterview r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await apps.Find(r.ApplicationId, ct) is not { } app)
            return ApplicationError.NotFound("Application not found");
        if (guard.Check(actor.Value!, AccessGuard.Of(app), AccessAction.InterviewSchedule) is { } denied)
            return denied;

        // the panel must be staff of the hiring company
        var panelIds = r.InterviewerIds.Distinct().Select(id => new UserId(id)).ToList();
        var panel = await users.FindMany(panelIds, ct);
        if (panel.Count != panelIds.Count || panel.Any(u => u.CompanyId != app.CompanyId))
            return ApplicationError.Validation("Interviewers must be staff of the hiring company");

        var interview = Interview.Schedule(
            InterviewId.New(), app, r.Kind, r.StartsAt, TimeSpan.FromMinutes(r.DurationMinutes), panelIds, clock.UtcNow);

        var busy = await interviews.ListScheduledFor(panelIds, interview.StartsAt, interview.EndsAt, ct);
        if (busy.Any(interview.Overlaps))
            return ApplicationError.Conflict("An interviewer already has an interview at that time");

        interviews.Add(interview);
        await uow.SaveChanges(ct);
        return (await new[] { interview }.ToDtos(users, ct))[0];
    }
}

// ── Cancel / feedback ───────────────────────────────────────────────────────

public sealed record CancelInterview(
    UserId ActorId,
    InterviewId InterviewId
) : ICommand<InterviewDto>;

public sealed class CancelInterviewHandler(
    AccessGuard guard,
    IApplicationRepository apps,
    IInterviewRepository interviews,
    IUserRepository users,
    IUnitOfWork uow
)
    : IHandler<CancelInterview, InterviewDto>
{
    public async Task<Result<InterviewDto>> Handle(CancelInterview r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await interviews.Find(r.InterviewId, ct) is not { } interview)
            return ApplicationError.NotFound("Interview not found");
        if (await apps.Find(interview.ApplicationId, ct) is not { } app)
            return ApplicationError.NotFound("Application not found");
        if (guard.Check(actor.Value!, AccessGuard.Of(app), AccessAction.InterviewSchedule) is { } denied)
            return denied;

        var cancelled = interview.Cancel();
        interviews.Update(cancelled);
        await uow.SaveChanges(ct);
        return (await new[] { cancelled }.ToDtos(users, ct))[0];
    }
}

public sealed record SubmitFeedback(
    UserId ActorId,
    InterviewId InterviewId,
    Recommendation Recommendation,
    string? Notes
)
    : ICommand<InterviewDto>;

public sealed class SubmitFeedbackHandler(
    IInterviewRepository interviews,
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<SubmitFeedback, InterviewDto>
{
    public async Task<Result<InterviewDto>> Handle(SubmitFeedback r, CancellationToken ct)
    {
        if (await interviews.Find(r.InterviewId, ct) is not { } interview)
            return ApplicationError.NotFound("Interview not found");
        // panel membership is the access rule; enforced by the aggregate
        if (!interview.Interviewers.Contains(r.ActorId))
            return ApplicationError.Forbidden("Only a panel interviewer can submit feedback");

        var completed = interview.Complete(r.ActorId, r.Recommendation, r.Notes, clock.UtcNow);
        interviews.Update(completed);
        await uow.SaveChanges(ct);
        return (await new[] { completed }.ToDtos(users, ct))[0];
    }
}

// ── Query ───────────────────────────────────────────────────────────────────

public sealed record ListApplicationInterviews(
    UserId ActorId,
    ApplicationId ApplicationId
) : IQuery<IReadOnlyList<InterviewDto>>;

public sealed class ListApplicationInterviewsHandler(
    AccessGuard guard,
    IApplicationRepository apps,
    IInterviewRepository interviews,
    IUserRepository users
)
    : IHandler<ListApplicationInterviews, IReadOnlyList<InterviewDto>>
{
    public async Task<Result<IReadOnlyList<InterviewDto>>> Handle(ListApplicationInterviews r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await apps.Find(r.ApplicationId, ct) is not { } app)
            return ApplicationError.NotFound("Application not found");
        if (guard.Check(actor.Value!, AccessGuard.Of(app), AccessAction.ApplicationRead) is not null)
            return ApplicationError.NotFound("Application not found");

        var list = await (await interviews.ListByApplication(app.Id, ct)).ToDtos(users, ct);
        // interviewer feedback is internal to the company
        return actor.Value!.CompanyId == app.CompanyId ? list : list.Select(i => i with { Feedback = null }).ToList();
    }
}
