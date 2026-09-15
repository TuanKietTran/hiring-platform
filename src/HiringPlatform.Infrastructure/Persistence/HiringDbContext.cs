using Microsoft.EntityFrameworkCore;

namespace HiringPlatform.Infrastructure.Persistence;

public sealed class HiringDbContext(
    DbContextOptions<HiringDbContext> options
) : DbContext(options)
{
    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<CompanyRow> Companies => Set<CompanyRow>();
    public DbSet<JobRow> Jobs => Set<JobRow>();
    public DbSet<ApplicationRow> Applications => Set<ApplicationRow>();
    public DbSet<ApplicantProfileRow> ApplicantProfiles => Set<ApplicantProfileRow>();
    public DbSet<InterviewRow> Interviews => Set<InterviewRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRow>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.CompanyId);
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
        });
        modelBuilder.Entity<CompanyRow>(e =>
        {
            e.ToTable("companies");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
        });
        modelBuilder.Entity<JobRow>(e =>
        {
            e.ToTable("jobs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CompanyId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
        });
        modelBuilder.Entity<ApplicantProfileRow>(e =>
        {
            e.ToTable("applicant_profiles");
            e.HasKey(x => x.CandidateId);
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
        });
        modelBuilder.Entity<ApplicationRow>(e =>
        {
            e.ToTable("applications");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.JobId);
            e.HasIndex(x => x.CandidateId);
            e.HasIndex(x => new { x.JobId, x.CandidateId }).IsUnique();
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
        });
        modelBuilder.Entity<InterviewRow>(e =>
        {
            e.ToTable("interviews");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ApplicationId);
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
        });
    }
}

public sealed class UserRow
{
    public Guid Id
    {
        get; set;
    }
    public string Email { get; set; } = ""; public Guid? CompanyId
    {
        get; set;
    }
    public string Snapshot { get; set; } = "{}";
}
public sealed class CompanyRow
{
    public Guid Id
    {
        get; set;
    }
    public string Slug { get; set; } = ""; public string Snapshot { get; set; } = "{}";
}
public sealed class JobRow
{
    public Guid Id
    {
        get; set;
    }
    public Guid CompanyId
    {
        get; set;
    }
    public int Status
    {
        get; set;
    }
    public string SearchText { get; set; } = ""; public string Snapshot { get; set; } = "{}";
}
public sealed class ApplicantProfileRow
{
    public Guid CandidateId
    {
        get; set;
    }
    public string Snapshot { get; set; } = "{}";
}
public sealed class ApplicationRow
{
    public Guid Id
    {
        get; set;
    }
    public Guid JobId
    {
        get; set;
    }
    public Guid CandidateId
    {
        get; set;
    }
    public Guid CompanyId
    {
        get; set;
    }
    public int Stage
    {
        get; set;
    }
    public string Snapshot { get; set; } = "{}";
}
public sealed class InterviewRow
{
    public Guid Id
    {
        get; set;
    }
    public Guid ApplicationId
    {
        get; set;
    }
    public Guid CompanyId
    {
        get; set;
    }
    public int Status
    {
        get; set;
    }
    public DateTimeOffset StartsAt
    {
        get; set;
    }
    public DateTimeOffset EndsAt
    {
        get; set;
    }
    public Guid[] InterviewerIds { get; set; } = []; public string Snapshot { get; set; } = "{}";
}
