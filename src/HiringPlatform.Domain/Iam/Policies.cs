namespace HiringPlatform.Domain.Iam;

using HiringPlatform.Domain.Identity;

public interface IPolicy
{
    string Name
    {
        get;
    }

    /// <summary>Return a decision, or null to abstain (policy not applicable).</summary>
    AccessDecision? Evaluate(AccessRequest request);
}

/// <summary>
/// Deny-overrides combinator:
///   1. Any explicit deny → deny.
///   2. No deny + at least one allow → allow.
///   3. All abstain → deny (default-deny).
/// </summary>
public sealed class PolicyEvaluator(
    IReadOnlyList<IPolicy> policies
)
{
    public static PolicyEvaluator Default
    {
        get;
    } = new(
    [
        new ServiceAccountReadOnlyPolicy(),
        new CandidateCannotSelfAdvancePolicy(),
        new OwnerFullAccessPolicy(),
        new CompanyStaffPolicy(),
        new CandidateApplyPolicy(),
        new PublicReadPolicy(),
    ]);

    public AccessDecision Evaluate(AccessRequest request)
    {
        var hasAllow = false;

        foreach (var policy in policies)
        {
            var decision = policy.Evaluate(request);
            if (decision is null)
                continue; // abstain
            if (!decision.IsAllowed)
                return AccessDecision.Deny($"[{policy.Name}] {decision.Reason}");
            hasAllow = true;
        }

        return hasAllow
            ? AccessDecision.Allow("At least one policy allowed the request")
            : AccessDecision.Deny("No policy allowed the request (default-deny)");
    }
}

// ---------------------------------------------------------------------------
// Built-in policies
// ---------------------------------------------------------------------------

/// <summary>Owners have full access to resources they own (candidate → own application/profile).</summary>
public sealed class OwnerFullAccessPolicy : IPolicy
{
    public string Name => nameof(OwnerFullAccessPolicy);

    public AccessDecision? Evaluate(AccessRequest request) =>
        request.Resource.OwnerUserId == request.Subject.UserId
            ? AccessDecision.Allow("Owner has full access to own resources")
            : null;
}

/// <summary>Service accounts may read anything they can reach; everything else is denied.</summary>
public sealed class ServiceAccountReadOnlyPolicy : IPolicy
{
    public string Name => nameof(ServiceAccountReadOnlyPolicy);

    public AccessDecision? Evaluate(AccessRequest request)
    {
        if (!request.Subject.IsServiceAccount)
            return null;
        return request.Action.IsReadOnly
            ? AccessDecision.Allow("Service account may read")
            : AccessDecision.Deny($"Service accounts are read-only; denied {request.Action}");
    }
}

/// <summary>
/// Owning an application must not let a candidate move it through the pipeline.
/// Explicit deny, so it overrides <see cref="OwnerFullAccessPolicy"/>.
/// </summary>
public sealed class CandidateCannotSelfAdvancePolicy : IPolicy
{
    public string Name => nameof(CandidateCannotSelfAdvancePolicy);

    public AccessDecision? Evaluate(AccessRequest request)
    {
        var (subject, resource, action) = (request.Subject, request.Resource, request.Action);
        var ownsApplication = resource.Type == ResourceType.Application && resource.OwnerUserId == subject.UserId;
        if (ownsApplication && (action == AccessAction.ApplicationAdvance || action == AccessAction.InterviewSchedule))
            return AccessDecision.Deny("Candidates cannot move their own application through the pipeline");
        return null;
    }
}

/// <summary>Staff manage their own company's jobs, applications and interviews. Only org admins edit the company.</summary>
public sealed class CompanyStaffPolicy : IPolicy
{
    public string Name => nameof(CompanyStaffPolicy);

    public AccessDecision? Evaluate(AccessRequest request)
    {
        var (subject, resource, action) = (request.Subject, request.Resource, request.Action);
        if (!subject.IsCompanyStaff || subject.CompanyId != resource.OwnerCompanyId)
            return null;

        if (action == AccessAction.CompanyWrite)
        {
            return subject.Role == Role.OrgAdmin
                ? AccessDecision.Allow("Org admin may edit the company")
                : AccessDecision.Deny("Only org admins may edit the company");
        }

        if (action == AccessAction.ApplicationCreate || action == AccessAction.ApplicationWithdraw)
            return null; // candidate-only actions

        return AccessDecision.Allow("Company staff may manage company resources");
    }
}

/// <summary>Candidates may apply to publicly listed jobs.</summary>
public sealed class CandidateApplyPolicy : IPolicy
{
    public string Name => nameof(CandidateApplyPolicy);

    public AccessDecision? Evaluate(AccessRequest request)
    {
        var (subject, resource, action) = (request.Subject, request.Resource, request.Action);
        if (action != AccessAction.ApplicationCreate || resource.Type != ResourceType.Job)
            return null;
        if (subject.Role != Role.Candidate)
            return null;
        return resource.IsPublic ? AccessDecision.Allow("Candidates may apply to listed jobs") : null;
    }
}

/// <summary>Anyone may read public jobs and companies. Deliberately narrow: other :read actions stay private.</summary>
public sealed class PublicReadPolicy : IPolicy
{
    public string Name => nameof(PublicReadPolicy);

    public AccessDecision? Evaluate(AccessRequest request)
    {
        var (resource, action) = (request.Resource, request.Action);
        if (!resource.IsPublic)
            return null;
        return action == AccessAction.JobRead || action == AccessAction.CompanyRead
            ? AccessDecision.Allow("Public resource")
            : null;
    }
}
