namespace HiringPlatform.Domain.Interviews;

using HiringPlatform.Domain.Applications;
using HiringPlatform.Domain.Common;

public enum InterviewKind
{
    Phone, Video, OnSite, Technical
}

public enum InterviewStatus
{
    Scheduled, Completed, Cancelled
}

public enum Recommendation
{
    StrongNo, No, Yes, StrongYes
}

public sealed record Feedback(
    UserId By,
    Recommendation Recommendation,
    string Notes,
    DateTimeOffset At
);

public sealed record Interview
{
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);

    private Interview()
    {
    }

    public InterviewId Id
    {
        get; private init;
    }
    public ApplicationId ApplicationId
    {
        get; private init;
    }
    public CompanyId CompanyId
    {
        get; private init;
    }
    public InterviewKind Kind
    {
        get; private init;
    }
    public DateTimeOffset StartsAt
    {
        get; private init;
    }
    public TimeSpan Duration
    {
        get; private init;
    }
    public IReadOnlyList<UserId> Interviewers { get; private init; } = [];
    public InterviewStatus Status
    {
        get; private init;
    }
    public Feedback? Feedback
    {
        get; private init;
    }

    public DateTimeOffset EndsAt => StartsAt + Duration;

    public static Interview Schedule(
        InterviewId id, JobApplication application, InterviewKind kind, DateTimeOffset startsAt, TimeSpan duration,
        IEnumerable<UserId> interviewers, DateTimeOffset now)
    {
        if (application.Stage != ApplicationStage.Interviewing)
            throw new DomainException("Interviews can only be scheduled for applications in the interviewing stage");
        if (startsAt <= now)
            throw new DomainException("Interview must start in the future");
        if (duration < MinDuration || duration > MaxDuration)
            throw new DomainException($"Interview duration must be between {MinDuration.TotalMinutes} minutes and {MaxDuration.TotalHours} hours");

        var panel = interviewers.Distinct().ToArray();
        if (panel.Length == 0)
            throw new DomainException("At least one interviewer is required");

        return new Interview
        {
            Id = id,
            ApplicationId = application.Id,
            CompanyId = application.CompanyId,
            Kind = kind,
            StartsAt = startsAt.ToUniversalTime(),
            Duration = duration,
            Interviewers = panel,
            Status = InterviewStatus.Scheduled,
        };
    }

    public bool Overlaps(Interview other) =>
        Status == InterviewStatus.Scheduled && other.Status == InterviewStatus.Scheduled
        && StartsAt < other.EndsAt && other.StartsAt < EndsAt;

    public Interview Cancel()
    {
        if (Status != InterviewStatus.Scheduled)
            throw new InvalidTransitionException(Status.ToString(), nameof(InterviewStatus.Cancelled));
        return this with
        {
            Status = InterviewStatus.Cancelled
        };
    }

    public Interview Complete(UserId by, Recommendation recommendation, string? notes, DateTimeOffset now)
    {
        if (Status != InterviewStatus.Scheduled)
            throw new InvalidTransitionException(Status.ToString(), nameof(InterviewStatus.Completed));
        if (!Interviewers.Contains(by))
            throw new DomainException("Only a panel interviewer can submit feedback");
        if (now < StartsAt)
            throw new DomainException("Feedback cannot be submitted before the interview starts");

        var text = notes?.Trim() ?? "";
        if (text.Length > 5_000)
            throw new DomainException("Feedback notes must be ≤ 5000 characters");

        return this with
        {
            Status = InterviewStatus.Completed,
            Feedback = new Feedback(by, recommendation, text, now.ToUniversalTime()),
        };
    }

    public InterviewSnapshot ToSnapshot() => new(
        Id.Value, ApplicationId.Value, CompanyId.Value, Kind, StartsAt, (int)Duration.TotalMinutes,
        [.. Interviewers.Select(i => i.Value)], Status,
        Feedback?.By.Value, Feedback?.Recommendation, Feedback?.Notes, Feedback?.At);

    public static Interview FromSnapshot(InterviewSnapshot s) => new()
    {
        Id = new InterviewId(s.Id),
        ApplicationId = new ApplicationId(s.ApplicationId),
        CompanyId = new CompanyId(s.CompanyId),
        Kind = s.Kind,
        StartsAt = s.StartsAt,
        Duration = TimeSpan.FromMinutes(s.DurationMinutes),
        Interviewers = [.. s.Interviewers.Select(i => new UserId(i))],
        Status = s.Status,
        Feedback = s is { FeedbackBy: { } by, Recommendation: { } rec, FeedbackAt: { } at }
            ? new Feedback(new UserId(by), rec, s.FeedbackNotes ?? "", at)
            : null,
    };
}

public sealed record InterviewSnapshot(
    Guid Id,
    Guid ApplicationId,
    Guid CompanyId,
    InterviewKind Kind,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    Guid[] Interviewers,
    InterviewStatus Status,
    Guid? FeedbackBy,
    Recommendation? Recommendation,
    string? FeedbackNotes,
    DateTimeOffset? FeedbackAt
);
