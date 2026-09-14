namespace HiringPlatform.Domain.Identity;

using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Iam;

public enum Role
{
    Candidate, Recruiter, HiringManager, OrgAdmin
}

public sealed record User
{
    private User()
    {
    }

    public UserId Id
    {
        get; private init;
    }
    public Email Email { get; private init; } = null!;
    public HashedPassword PasswordHash { get; private init; } = null!;
    public string FullName { get; private init; } = "";
    public Role Role
    {
        get; private init;
    }
    public CompanyId? CompanyId
    {
        get; private init;
    }
    public bool IsServiceAccount
    {
        get; private init;
    }
    public DateTimeOffset CreatedAt
    {
        get; private init;
    }

    public bool IsCompanyStaff => Role != Role.Candidate;

    public static User RegisterCandidate(UserId id, Email email, HashedPassword hash, string fullName, DateTimeOffset now) =>
        Create(id, email, hash, fullName, Role.Candidate, companyId: null, now);

    public static User RegisterStaff(
        UserId id, Email email, HashedPassword hash, string fullName, CompanyId companyId, Role role, DateTimeOffset now)
    {
        if (role == Role.Candidate)
            throw new DomainException("staff accounts need a staff role");
        return Create(id, email, hash, fullName, role, companyId, now);
    }

    private static User Create(
        UserId id, Email email, HashedPassword hash, string fullName, Role role, CompanyId? companyId, DateTimeOffset now)
    {
        var name = fullName?.Trim() ?? "";
        if (name.Length == 0)
            throw new DomainException("Full name must not be empty");
        if (name.Length > 120)
            throw new DomainException("Full name must be ≤ 120 characters");

        return new User
        {
            Id = id,
            Email = email,
            PasswordHash = hash,
            FullName = name,
            Role = role,
            CompanyId = companyId,
            IsServiceAccount = false,
            CreatedAt = now.ToUniversalTime(),
        };
    }

    public SubjectAttributes ToSubject() => new(Id, CompanyId, Role, IsServiceAccount);

    public UserSnapshot ToSnapshot() =>
        new(Id.Value, Email.Value, PasswordHash.Hash, FullName, Role, CompanyId?.Value, IsServiceAccount, CreatedAt);

    public static User FromSnapshot(UserSnapshot s) => new()
    {
        Id = new UserId(s.Id),
        Email = Email.Create(s.Email),
        PasswordHash = HashedPassword.FromHash(s.PasswordHash),
        FullName = s.FullName,
        Role = s.Role,
        CompanyId = s.CompanyId is { } c ? new CompanyId(c) : null,
        IsServiceAccount = s.IsServiceAccount,
        CreatedAt = s.CreatedAt,
    };
}

public sealed record UserSnapshot(
    Guid Id,
    string Email,
    string PasswordHash,
    string FullName,
    Role Role,
    Guid? CompanyId,
    bool IsServiceAccount,
    DateTimeOffset CreatedAt
);
