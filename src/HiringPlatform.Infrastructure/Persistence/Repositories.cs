using System.Text.Json;

using HiringPlatform.Application.Common;
using HiringPlatform.Domain.Applicants;
using HiringPlatform.Domain.Applications;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Companies;
using HiringPlatform.Domain.Identity;
using HiringPlatform.Domain.Interviews;
using HiringPlatform.Domain.Jobs;

using Microsoft.EntityFrameworkCore;

namespace HiringPlatform.Infrastructure.Persistence;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
}

public sealed class UnitOfWork(
    HiringDbContext db
) : IUnitOfWork
{
    public async Task SaveChanges(CancellationToken ct) => await db.SaveChangesAsync(ct);
}

public sealed class UserRepository(
    HiringDbContext db
) : IUserRepository
{
    public async Task<User?> Find(UserId id, CancellationToken ct) => Map(await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, ct));
    public async Task<User?> FindByEmail(Email email, CancellationToken ct) => Map(await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email.Value, ct));
    public async Task<IReadOnlyList<User>> FindMany(IEnumerable<UserId> ids, CancellationToken ct)
    {
        var values = ids.Select(x => x.Value).Distinct().ToArray();
        return (await db.Users.AsNoTracking().Where(x => values.Contains(x.Id)).ToListAsync(ct)).Select(Map).OfType<User>().ToList();
    }
    public async Task<IReadOnlyList<User>> ListStaff(CompanyId companyId, CancellationToken ct) =>
        (await db.Users.AsNoTracking().Where(x => x.CompanyId == companyId.Value).OrderBy(x => x.Email).ToListAsync(ct)).Select(Map).OfType<User>().ToList();
    public void Add(User user) => db.Users.Add(Row(user));
    private static User? Map(UserRow? row) => row is null ? null : User.FromSnapshot(Json.Read<UserSnapshot>(row.Snapshot));
    private static UserRow Row(User u) => new() { Id = u.Id.Value, Email = u.Email.Value, CompanyId = u.CompanyId?.Value, Snapshot = Json.Write(u.ToSnapshot()) };
}

public sealed class CompanyRepository(
    HiringDbContext db
) : ICompanyRepository
{
    public async Task<Company?> Find(CompanyId id, CancellationToken ct) => Map(await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, ct));
    public async Task<IReadOnlyList<Company>> FindMany(IEnumerable<CompanyId> ids, CancellationToken ct)
    {
        var values = ids.Select(x => x.Value).Distinct().ToArray();
        return (await db.Companies.AsNoTracking().Where(x => values.Contains(x.Id)).ToListAsync(ct)).Select(Map).OfType<Company>().ToList();
    }
    public Task<bool> SlugExists(string slug, CancellationToken ct) => db.Companies.AnyAsync(x => x.Slug == slug, ct);
    public void Add(Company company) => db.Companies.Add(new CompanyRow { Id = company.Id.Value, Slug = company.Slug, Snapshot = Json.Write(company.ToSnapshot()) });
    private static Company? Map(CompanyRow? row) => row is null ? null : Company.FromSnapshot(Json.Read<CompanySnapshot>(row.Snapshot));
}

public sealed class JobRepository(
    HiringDbContext db
) : IJobRepository
{
    public async Task<Job?> Find(JobId id, CancellationToken ct) => Map(await db.Jobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, ct));
    public async Task<IReadOnlyList<Job>> FindMany(IEnumerable<JobId> ids, CancellationToken ct)
    {
        var values = ids.Select(x => x.Value).Distinct().ToArray();
        return (await db.Jobs.AsNoTracking().Where(x => values.Contains(x.Id)).ToListAsync(ct)).Select(Map).OfType<Job>().ToList();
    }
    public async Task<IReadOnlyList<Job>> ListByCompany(CompanyId companyId, CancellationToken ct) =>
        (await db.Jobs.AsNoTracking().Where(x => x.CompanyId == companyId.Value).OrderByDescending(x => x.Id).ToListAsync(ct)).Select(Map).OfType<Job>().ToList();
    public async Task<(IReadOnlyList<Job> Items, int Total)> SearchOpen(JobSearch search, CancellationToken ct)
    {
        var q = db.Jobs.AsNoTracking().Where(x => x.Status == (int)JobStatus.Open);
        if (!string.IsNullOrWhiteSpace(search.Text))
            q = q.Where(x => EF.Functions.ILike(x.SearchText, $"%{search.Text}%"));
        // workplace filtering is performed after deserialization for this compact adapter
        var rows = await q.OrderByDescending(x => x.Id).ToListAsync(ct);
        var jobs = rows.Select(Map).OfType<Job>();
        if (search.Workplace is { } workplace)
            jobs = jobs.Where(x => x.WorkplaceType == workplace);
        var list = jobs.ToList();
        return (list.Skip((search.Page - 1) * search.PageSize).Take(search.PageSize).ToList(), list.Count);
    }
    public void Add(Job job) => db.Jobs.Add(Row(job));
    public void Update(Job job) => db.Jobs.Update(Row(job));
    private static Job? Map(JobRow? row) => row is null ? null : Job.FromSnapshot(Json.Read<JobSnapshot>(row.Snapshot));
    private static JobRow Row(Job j) => new() { Id = j.Id.Value, CompanyId = j.CompanyId.Value, Status = (int)j.Status, SearchText = $"{j.Title} {j.Description} {j.Location} {string.Join(' ', j.Skills)}", Snapshot = Json.Write(j.ToSnapshot()) };
}

public sealed class ApplicantProfileRepository(
    HiringDbContext db
) : IApplicantProfileRepository
{
    public async Task<CandidateProfile?> Find(UserId candidateId, CancellationToken ct)
    {
        var row = await db.ApplicantProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.CandidateId == candidateId.Value, ct);
        return row is null ? null : CandidateProfile.FromSnapshot(Json.Read<CandidateProfileSnapshot>(row.Snapshot));
    }
    public void Add(CandidateProfile profile) => db.ApplicantProfiles.Add(Row(profile));
    public void Update(CandidateProfile profile) => db.ApplicantProfiles.Update(Row(profile));
    private static ApplicantProfileRow Row(CandidateProfile profile) => new()
    {
        CandidateId = profile.CandidateId.Value,
        Snapshot = Json.Write(profile.ToSnapshot()),
    };
}

public sealed class ApplicationRepository(
    HiringDbContext db
) : IApplicationRepository
{
    public async Task<JobApplication?> Find(ApplicationId id, CancellationToken ct) => Map(await db.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, ct));
    public Task<bool> Exists(JobId jobId, UserId candidateId, CancellationToken ct) => db.Applications.AnyAsync(x => x.JobId == jobId.Value && x.CandidateId == candidateId.Value, ct);
    public async Task<IReadOnlyList<JobApplication>> ListByCandidate(UserId candidateId, CancellationToken ct) => Map(await db.Applications.AsNoTracking().Where(x => x.CandidateId == candidateId.Value).OrderByDescending(x => x.Id).ToListAsync(ct));
    public async Task<IReadOnlyList<JobApplication>> ListByJob(JobId jobId, CancellationToken ct) => Map(await db.Applications.AsNoTracking().Where(x => x.JobId == jobId.Value).OrderByDescending(x => x.Id).ToListAsync(ct));
    public void Add(JobApplication application) => db.Applications.Add(Row(application));
    public void Update(JobApplication application) => db.Applications.Update(Row(application));
    private static JobApplication? Map(ApplicationRow? row) => row is null ? null : JobApplication.FromSnapshot(Json.Read<ApplicationSnapshot>(row.Snapshot));
    private static List<JobApplication> Map(IEnumerable<ApplicationRow> rows) => rows.Select(Map).OfType<JobApplication>().ToList();
    private static ApplicationRow Row(JobApplication a) => new() { Id = a.Id.Value, JobId = a.JobId.Value, CandidateId = a.CandidateId.Value, CompanyId = a.CompanyId.Value, Stage = (int)a.Stage, Snapshot = Json.Write(a.ToSnapshot()) };
}

public sealed class InterviewRepository(
    HiringDbContext db
) : IInterviewRepository
{
    public async Task<Interview?> Find(InterviewId id, CancellationToken ct) => Map(await db.Interviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, ct));
    public async Task<IReadOnlyList<Interview>> ListByApplication(ApplicationId applicationId, CancellationToken ct) => Map(await db.Interviews.AsNoTracking().Where(x => x.ApplicationId == applicationId.Value).OrderBy(x => x.StartsAt).ToListAsync(ct));
    public async Task<IReadOnlyList<Interview>> ListScheduledFor(IEnumerable<UserId> interviewers, DateTimeOffset from, DateTimeOffset until, CancellationToken ct)
    {
        var values = interviewers.Select(x => x.Value).Distinct().ToArray();
        var rows = await db.Interviews.AsNoTracking().Where(x => x.Status == (int)InterviewStatus.Scheduled && x.StartsAt < until && from < x.EndsAt).ToListAsync(ct);
        return Map(rows.Where(x => x.InterviewerIds.Any(values.Contains)));
    }
    public void Add(Interview interview) => db.Interviews.Add(Row(interview));
    public void Update(Interview interview) => db.Interviews.Update(Row(interview));
    private static Interview? Map(InterviewRow? row) => row is null ? null : Interview.FromSnapshot(Json.Read<InterviewSnapshot>(row.Snapshot));
    private static List<Interview> Map(IEnumerable<InterviewRow> rows) => rows.Select(Map).OfType<Interview>().ToList();
    private static InterviewRow Row(Interview i) => new() { Id = i.Id.Value, ApplicationId = i.ApplicationId.Value, CompanyId = i.CompanyId.Value, Status = (int)i.Status, StartsAt = i.StartsAt, EndsAt = i.EndsAt, InterviewerIds = [.. i.Interviewers.Select(x => x.Value)], Snapshot = Json.Write(i.ToSnapshot()) };
}
