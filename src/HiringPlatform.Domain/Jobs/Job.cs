namespace HiringPlatform.Domain.Jobs;

using HiringPlatform.Domain.Common;

public enum EmploymentType
{
    FullTime, PartTime, Contract, Internship
}

public enum WorkplaceType
{
    OnSite, Hybrid, Remote
}

public enum JobStatus
{
    Draft, Open, Paused, Closed
}

/// <summary>Min/max compensation in one currency. Built on the Money value object ported from cv-sv.</summary>
public sealed record SalaryRange
{
    private SalaryRange(Money min, Money max)
    {
        Min = min;
        Max = max;
    }

    public Money Min
    {
        get;
    }
    public Money Max
    {
        get;
    }

    public static SalaryRange Of(Money min, Money max)
    {
        if (min.IsGreaterThan(max))
            throw new DomainException("salary min must be ≤ max");
        return new SalaryRange(min, max);
    }
}

/// <summary>Status transitions, same table-driven style as cv-sv SubscriptionStatus.</summary>
public static class JobStatusRules
{
    private static readonly Dictionary<JobStatus, JobStatus[]> Transitions = new()
    {
        [JobStatus.Draft] = [JobStatus.Open, JobStatus.Closed],
        [JobStatus.Open] = [JobStatus.Paused, JobStatus.Closed],
        [JobStatus.Paused] = [JobStatus.Open, JobStatus.Closed],
        [JobStatus.Closed] = [],
    };

    public static JobStatus TransitionTo(this JobStatus from, JobStatus to) =>
        Transitions[from].Contains(to) ? to : throw new InvalidTransitionException(from.ToString(), to.ToString());

    public static bool AcceptsApplications(this JobStatus status) => status == JobStatus.Open;
}

public sealed record Job
{
    private Job()
    {
    }

    public JobId Id
    {
        get; private init;
    }
    public CompanyId CompanyId
    {
        get; private init;
    }
    public UserId CreatedBy
    {
        get; private init;
    }
    public string Title { get; private init; } = "";
    public string Description { get; private init; } = "";
    public string Location { get; private init; } = "";
    public EmploymentType EmploymentType
    {
        get; private init;
    }
    public WorkplaceType WorkplaceType
    {
        get; private init;
    }
    public SalaryRange? Salary
    {
        get; private init;
    }
    public IReadOnlyList<string> Skills { get; private init; } = [];
    public JobStatus Status
    {
        get; private init;
    }
    public DateTimeOffset CreatedAt
    {
        get; private init;
    }
    public DateTimeOffset? PublishedAt
    {
        get; private init;
    }
    public DateTimeOffset? ClosedAt
    {
        get; private init;
    }

    public bool IsPublic => Status == JobStatus.Open;

    public static Job Draft(
        JobId id, CompanyId companyId, UserId createdBy, JobDetails details, DateTimeOffset now) =>
        Apply(new Job
        {
            Id = id,
            CompanyId = companyId,
            CreatedBy = createdBy,
            Status = JobStatus.Draft,
            CreatedAt = now.ToUniversalTime(),
        }, details);

    // === transitions — all return a new Job ===

    public Job Edit(JobDetails details)
    {
        if (Status == JobStatus.Closed)
            throw new DomainException("Closed jobs cannot be edited");
        return Apply(this, details);
    }

    public Job Publish(DateTimeOffset now) =>
        this with
        {
            Status = Status.TransitionTo(JobStatus.Open),
            PublishedAt = PublishedAt ?? now.ToUniversalTime()
        };

    public Job Pause() => this with { Status = Status.TransitionTo(JobStatus.Paused) };

    public Job Close(DateTimeOffset now) =>
        this with
        {
            Status = Status.TransitionTo(JobStatus.Closed),
            ClosedAt = now.ToUniversalTime()
        };

    private static Job Apply(Job job, JobDetails d)
    {
        var title = d.Title?.Trim() ?? "";
        if (title.Length == 0)
            throw new DomainException("Job title must not be empty");
        if (title.Length > 150)
            throw new DomainException("Job title must be ≤ 150 characters");

        var description = d.Description?.Trim() ?? "";
        if (description.Length > 20_000)
            throw new DomainException("Job description must be ≤ 20000 characters");

        return job with
        {
            Title = title,
            Description = description,
            Location = d.Location?.Trim() ?? "",
            EmploymentType = d.EmploymentType,
            WorkplaceType = d.WorkplaceType,
            Salary = d.Salary,
            Skills = Common.Skills.Normalize(d.Skills),
        };
    }

    public JobSnapshot ToSnapshot() => new(
        Id.Value, CompanyId.Value, CreatedBy.Value, Title, Description, Location, EmploymentType, WorkplaceType,
        Salary?.Min.AmountMinor, Salary?.Max.AmountMinor, Salary?.Min.Currency,
        [.. Skills], Status, CreatedAt, PublishedAt, ClosedAt);

    public static Job FromSnapshot(JobSnapshot s) => new()
    {
        Id = new JobId(s.Id),
        CompanyId = new CompanyId(s.CompanyId),
        CreatedBy = new UserId(s.CreatedBy),
        Title = s.Title,
        Description = s.Description,
        Location = s.Location,
        EmploymentType = s.EmploymentType,
        WorkplaceType = s.WorkplaceType,
        Salary = s is { SalaryMinMinor: { } min, SalaryMaxMinor: { } max, SalaryCurrency: { } cur }
            ? SalaryRange.Of(Money.Of(min, cur), Money.Of(max, cur))
            : null,
        Skills = s.Skills,
        Status = s.Status,
        CreatedAt = s.CreatedAt,
        PublishedAt = s.PublishedAt,
        ClosedAt = s.ClosedAt,
    };
}

public sealed record JobDetails(
    string Title,
    string Description,
    string Location,
    EmploymentType EmploymentType,
    WorkplaceType WorkplaceType,
    SalaryRange? Salary,
    IEnumerable<string>? Skills
);

public sealed record JobSnapshot(
    Guid Id,
    Guid CompanyId,
    Guid CreatedBy,
    string Title,
    string Description,
    string Location,
    EmploymentType EmploymentType,
    WorkplaceType WorkplaceType,
    long? SalaryMinMinor,
    long? SalaryMaxMinor,
    Currency? SalaryCurrency,
    string[] Skills,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ClosedAt
);
