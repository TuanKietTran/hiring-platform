using HiringPlatform.Domain.Applicants;
using HiringPlatform.Domain.Applications;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Companies;
using HiringPlatform.Domain.Identity;
using HiringPlatform.Domain.Interviews;
using HiringPlatform.Domain.Jobs;

namespace HiringPlatform.Application.Common;

// Repository ports (cv-sv core/repos). Adapters live in Infrastructure.
// Add/Update stage changes; IUnitOfWork.SaveChanges commits them together.

public interface IClock
{
    DateTimeOffset UtcNow
    {
        get;
    }
}

public interface IUnitOfWork
{
    Task SaveChanges(CancellationToken ct);
}

public interface IUserRepository
{
    Task<User?> Find(UserId id, CancellationToken ct);
    Task<User?> FindByEmail(Email email, CancellationToken ct);
    Task<IReadOnlyList<User>> FindMany(IEnumerable<UserId> ids, CancellationToken ct);
    Task<IReadOnlyList<User>> ListStaff(CompanyId companyId, CancellationToken ct);
    void Add(User user);
}

public interface ICompanyRepository
{
    Task<Company?> Find(CompanyId id, CancellationToken ct);
    Task<IReadOnlyList<Company>> FindMany(IEnumerable<CompanyId> ids, CancellationToken ct);
    Task<bool> SlugExists(string slug, CancellationToken ct);
    void Add(Company company);
}

public sealed record JobSearch(
    string? Text,
    WorkplaceType? Workplace,
    int Page,
    int PageSize
);

public interface IJobRepository
{
    Task<Job?> Find(JobId id, CancellationToken ct);
    Task<IReadOnlyList<Job>> FindMany(IEnumerable<JobId> ids, CancellationToken ct);
    Task<IReadOnlyList<Job>> ListByCompany(CompanyId companyId, CancellationToken ct);
    Task<(IReadOnlyList<Job> Items, int Total)> SearchOpen(JobSearch search, CancellationToken ct);
    void Add(Job job);
    void Update(Job job);
}

public interface IApplicantProfileRepository
{
    Task<CandidateProfile?> Find(UserId candidateId, CancellationToken ct);
    void Add(CandidateProfile profile);
    void Update(CandidateProfile profile);
}

public interface IApplicationRepository
{
    Task<JobApplication?> Find(ApplicationId id, CancellationToken ct);
    Task<bool> Exists(JobId jobId, UserId candidateId, CancellationToken ct);
    Task<IReadOnlyList<JobApplication>> ListByCandidate(UserId candidateId, CancellationToken ct);
    Task<IReadOnlyList<JobApplication>> ListByJob(JobId jobId, CancellationToken ct);
    void Add(JobApplication application);
    void Update(JobApplication application);
}

public interface IInterviewRepository
{
    Task<Interview?> Find(InterviewId id, CancellationToken ct);
    Task<IReadOnlyList<Interview>> ListByApplication(ApplicationId applicationId, CancellationToken ct);

    /// <summary>Scheduled interviews involving any of the given interviewers that intersect [from, to).</summary>
    Task<IReadOnlyList<Interview>> ListScheduledFor(IEnumerable<UserId> interviewers, DateTimeOffset from, DateTimeOffset until, CancellationToken ct);

    void Add(Interview interview);
    void Update(Interview interview);
}
