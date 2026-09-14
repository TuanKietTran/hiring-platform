namespace HiringPlatform.Domain.Applications;

using HiringPlatform.Domain.Common;

// Pipeline, same table-driven shape as cv-sv SubscriptionStatus:
//
//   applied ──► screening ──► interviewing ──► offered ──► hired
//      │            │              │              │
//      └────────────┴──────────────┴──────────────┴──► rejected | withdrawn
public enum ApplicationStage
{
    Applied, Screening, Interviewing, Offered, Hired, Rejected, Withdrawn
}

public static class ApplicationStageRules
{
    private static readonly ApplicationStage[] Exits = [ApplicationStage.Rejected, ApplicationStage.Withdrawn];

    private static readonly Dictionary<ApplicationStage, ApplicationStage[]> Transitions = new()
    {
        [ApplicationStage.Applied] = [ApplicationStage.Screening, .. Exits],
        [ApplicationStage.Screening] = [ApplicationStage.Interviewing, .. Exits],
        [ApplicationStage.Interviewing] = [ApplicationStage.Offered, .. Exits],
        [ApplicationStage.Offered] = [ApplicationStage.Hired, .. Exits],
        [ApplicationStage.Hired] = [],
        [ApplicationStage.Rejected] = [],
        [ApplicationStage.Withdrawn] = [],
    };

    public static bool IsTerminal(this ApplicationStage stage) => Transitions[stage].Length == 0;

    public static bool CanTransitionTo(this ApplicationStage from, ApplicationStage to) => Transitions[from].Contains(to);

    public static ApplicationStage TransitionTo(this ApplicationStage from, ApplicationStage to) =>
        from.CanTransitionTo(to) ? to : throw new InvalidTransitionException(from.ToString(), to.ToString());
}

public sealed record StageChange(
    ApplicationStage From,
    ApplicationStage To,
    UserId ChangedBy,
    string? Note,
    DateTimeOffset At
);

public sealed record JobApplication
{
    private JobApplication()
    {
    }

    public ApplicationId Id
    {
        get; private init;
    }
    public JobId JobId
    {
        get; private init;
    }
    public CompanyId CompanyId
    {
        get; private init;
    }
    public UserId CandidateId
    {
        get; private init;
    }
    public string CoverLetter { get; private init; } = "";
    public Uri? ResumeUrl
    {
        get; private init;
    }
    public ApplicationStage Stage
    {
        get; private init;
    }
    public IReadOnlyList<StageChange> History { get; private init; } = [];
    public DateTimeOffset AppliedAt
    {
        get; private init;
    }

    public static JobApplication Submit(
        ApplicationId id, JobId jobId, CompanyId companyId, UserId candidateId,
        string? coverLetter, string? resumeUrl, DateTimeOffset now)
    {
        var letter = coverLetter?.Trim() ?? "";
        if (letter.Length > 5_000)
            throw new DomainException("Cover letter must be ≤ 5000 characters");

        Uri? resume = null;
        if (!string.IsNullOrWhiteSpace(resumeUrl)
            && (!Uri.TryCreate(resumeUrl.Trim(), UriKind.Absolute, out resume) || resume.Scheme != Uri.UriSchemeHttps))
            throw new DomainException("Resume URL must be an absolute https URL");

        return new JobApplication
        {
            Id = id,
            JobId = jobId,
            CompanyId = companyId,
            CandidateId = candidateId,
            CoverLetter = letter,
            ResumeUrl = resume,
            Stage = ApplicationStage.Applied,
            AppliedAt = now.ToUniversalTime(),
        };
    }

    public JobApplication Advance(ApplicationStage to, UserId by, string? note, DateTimeOffset now)
    {
        if (to == ApplicationStage.Withdrawn)
            throw new DomainException("Use Withdraw for candidate withdrawal");
        return Move(to, by, note, now);
    }

    public JobApplication Withdraw(UserId by, DateTimeOffset now)
    {
        if (by != CandidateId)
            throw new DomainException("Only the candidate can withdraw an application");
        return Move(ApplicationStage.Withdrawn, by, null, now);
    }

    private JobApplication Move(ApplicationStage to, UserId by, string? note, DateTimeOffset now)
    {
        var next = Stage.TransitionTo(to);
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > 2_000)
            throw new DomainException("Note must be ≤ 2000 characters");

        return this with
        {
            Stage = next,
            History = [.. History, new StageChange(Stage, next, by, trimmed, now.ToUniversalTime())],
        };
    }

    public ApplicationSnapshot ToSnapshot() => new(
        Id.Value, JobId.Value, CompanyId.Value, CandidateId.Value, CoverLetter, ResumeUrl?.ToString(), Stage,
        [.. History.Select(h => new StageChangeSnapshot(h.From, h.To, h.ChangedBy.Value, h.Note, h.At))], AppliedAt);

    public static JobApplication FromSnapshot(ApplicationSnapshot s) => new()
    {
        Id = new ApplicationId(s.Id),
        JobId = new JobId(s.JobId),
        CompanyId = new CompanyId(s.CompanyId),
        CandidateId = new UserId(s.CandidateId),
        CoverLetter = s.CoverLetter,
        ResumeUrl = s.ResumeUrl is null ? null : new Uri(s.ResumeUrl),
        Stage = s.Stage,
        History = [.. s.History.Select(h => new StageChange(h.From, h.To, new UserId(h.ChangedBy), h.Note, h.At))],
        AppliedAt = s.AppliedAt,
    };
}

public sealed record StageChangeSnapshot(
    ApplicationStage From,
    ApplicationStage To,
    Guid ChangedBy,
    string? Note,
    DateTimeOffset At
);

public sealed record ApplicationSnapshot(
    Guid Id,
    Guid JobId,
    Guid CompanyId,
    Guid CandidateId,
    string CoverLetter,
    string? ResumeUrl,
    ApplicationStage Stage,
    StageChangeSnapshot[] History,
    DateTimeOffset AppliedAt
);
