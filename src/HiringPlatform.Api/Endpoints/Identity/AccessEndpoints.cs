using System.Security.Claims;

using HiringPlatform.Application.Common;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Iam;

namespace HiringPlatform.Api;

/// <summary>Central policy-decision endpoint. Services send normalized resource attributes; deny overrides.</summary>
internal static class AccessEndpoints
{
    public static RouteGroupBuilder MapAccessEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/access/evaluate", Evaluate).RequireAuthorization();
        return api;
    }

    private static async Task<IResult> Evaluate(
        EvaluateAccessBody body, ClaimsPrincipal principal, IUserRepository users, CancellationToken ct)
    {
        var actor = await users.Find(principal.UserId(), ct);
        if (actor is null)
            return Results.Unauthorized();

        try
        {
            var resource = new ResourceAttributes(
                body.ResourceType, body.ResourceId,
                body.OwnerUserId is { } owner ? new UserId(owner) : null,
                body.OwnerCompanyId is { } company ? new CompanyId(company) : null,
                body.IsPublic);
            var decision = PolicyEvaluator.Default.Evaluate(
                new AccessRequest(actor.ToSubject(), resource, AccessAction.FromCode(body.Action), DateTimeOffset.UtcNow));
            return Results.Ok(new
            {
                allowed = decision.IsAllowed,
                effect = decision.Effect,
                decision.Reason
            });
        }
        catch (DomainException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [ex.Message] });
        }
    }
}

internal sealed record EvaluateAccessBody(
    ResourceType ResourceType,
    Guid ResourceId,
    Guid? OwnerUserId,
    Guid? OwnerCompanyId,
    bool IsPublic,
    string Action
);
