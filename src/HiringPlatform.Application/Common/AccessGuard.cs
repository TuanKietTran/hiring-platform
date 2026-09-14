using HiringPlatform.Domain.Applications;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Companies;
using HiringPlatform.Domain.Iam;
using HiringPlatform.Domain.Identity;
using HiringPlatform.Domain.Jobs;

namespace HiringPlatform.Application.Common;

/// <summary>
/// Enforcement point for the ABAC policies (cv-sv CheckAccess). Every handler that touches a
/// resource on behalf of a user goes through here; the UI only mirrors these rules.
/// </summary>
public sealed class AccessGuard(
    IUserRepository users,
    IClock clock
)
{
    private readonly PolicyEvaluator _evaluator = PolicyEvaluator.Default;

    public async Task<Result<User>> Actor(UserId actorId, CancellationToken ct) =>
        await users.Find(actorId, ct) is { } user ? user : ApplicationError.Unauthorized("Unknown user");

    public ApplicationError? Check(User actor, ResourceAttributes resource, AccessAction action)
    {
        var decision = _evaluator.Evaluate(new AccessRequest(actor.ToSubject(), resource, action, clock.UtcNow));
        return decision.IsAllowed ? null : ApplicationError.Forbidden(decision.Reason);
    }

    public static ResourceAttributes Of(Company c) =>
        new(ResourceType.Company, c.Id.Value, OwnerUserId: null, OwnerCompanyId: c.Id, IsPublic: true);

    public static ResourceAttributes Of(Job j) =>
        new(ResourceType.Job, j.Id.Value, OwnerUserId: null, OwnerCompanyId: j.CompanyId, IsPublic: j.IsPublic);

    public static ResourceAttributes Of(JobApplication a) =>
        new(ResourceType.Application, a.Id.Value, OwnerUserId: a.CandidateId, OwnerCompanyId: a.CompanyId);

    public static ResourceAttributes OwnCompany(CompanyId id) =>
        new(ResourceType.Company, id.Value, OwnerUserId: null, OwnerCompanyId: id);
}
