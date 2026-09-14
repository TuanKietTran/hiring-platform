namespace HiringPlatform.Domain.Iam;

using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Identity;

// ABAC model ported from cv-sv core/domain/iam, re-targeted at hiring resources.

public enum ResourceType
{
    Company, Job, Application, CandidateProfile
}

public sealed record AccessAction
{
    private AccessAction(string code) => Code = code;

    public string Code
    {
        get;
    }

    public bool IsReadOnly => Code.EndsWith(":read", StringComparison.Ordinal);

    public static readonly AccessAction CompanyRead = new("company:read");
    public static readonly AccessAction CompanyWrite = new("company:write");
    public static readonly AccessAction JobRead = new("job:read");
    public static readonly AccessAction JobWrite = new("job:write");
    public static readonly AccessAction JobPublish = new("job:publish");
    public static readonly AccessAction ApplicationRead = new("application:read");
    public static readonly AccessAction ApplicationCreate = new("application:create");
    public static readonly AccessAction ApplicationAdvance = new("application:advance");
    public static readonly AccessAction ApplicationWithdraw = new("application:withdraw");
    public static readonly AccessAction InterviewSchedule = new("interview:schedule");
    public static readonly AccessAction CandidateRead = new("candidate:read");
    public static readonly AccessAction CandidateWrite = new("candidate:write");

    public override string ToString() => Code;
}

public sealed record SubjectAttributes(
    UserId UserId,
    CompanyId? CompanyId,
    Role Role,
    bool IsServiceAccount
)
{
    public bool IsCompanyStaff => Role != Role.Candidate && CompanyId is not null;
}

public sealed record ResourceAttributes(
    ResourceType Type,
    Guid Id,
    UserId? OwnerUserId,
    CompanyId? OwnerCompanyId,
    bool IsPublic = false
);

public sealed record AccessRequest(
    SubjectAttributes Subject,
    ResourceAttributes Resource,
    AccessAction Action,
    DateTimeOffset RequestedAt
);

public enum Effect
{
    Allow, Deny
}

public sealed record AccessDecision(
    Effect Effect,
    string Reason
)
{
    public static AccessDecision Allow(string reason) => new(Effect.Allow, reason);
    public static AccessDecision Deny(string reason) => new(Effect.Deny, reason);

    public bool IsAllowed => Effect == Effect.Allow;

    public override string ToString() => $"{Effect}: {Reason}";
}
