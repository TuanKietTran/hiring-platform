using System.Security.Claims;

using HiringPlatform.Application.Common;
using HiringPlatform.Application.Jobs;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Jobs;

namespace HiringPlatform.Api;

internal static class JobEndpoints
{
    public static RouteGroupBuilder MapJobEndpoints(this RouteGroupBuilder api) =>
        api.MapPublicJobEndpoints().MapRecruiterJobEndpoints();

    public static RouteGroupBuilder MapPublicJobEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/jobs", SearchJobs);
        api.MapGet("/jobs/{id:guid}", GetJob);
        return api;
    }

    public static RouteGroupBuilder MapRecruiterJobEndpoints(this RouteGroupBuilder api)
    {
        var secured = api.MapGroup("").RequireAuthorization();
        secured.MapGet("/company/jobs", ListCompanyJobs);
        secured.MapPost("/jobs", CreateJob);
        secured.MapPut("/jobs/{id:guid}", UpdateJob);
        secured.MapPost("/jobs/{id:guid}/status", ChangeJobStatus);
        return api;
    }

    private static async Task<IResult> SearchJobs(
        string? q,
        WorkplaceType? workplace,
        int page,
        int pageSize,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(
            new SearchJobs(q, workplace, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct)).ToHttp();

    private static async Task<IResult> GetJob(
        Guid id,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct)
    {
        UserId? actor = principal.Identity?.IsAuthenticated == true ? principal.UserId() : null;
        return (await mediator.Send(new GetJob(actor, new JobId(id)), ct)).ToHttp();
    }

    private static async Task<IResult> ListCompanyJobs(
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new ListCompanyJobs(principal.UserId()), ct)).ToHttp();

    private static async Task<IResult> CreateJob(
        JobInput body,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new CreateJob(principal.UserId(), body), ct)).ToHttp();

    private static async Task<IResult> UpdateJob(
        Guid id,
        JobInput body,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new UpdateJob(principal.UserId(), new JobId(id), body), ct)).ToHttp();

    private static async Task<IResult> ChangeJobStatus(
        Guid id,
        JobStatusBody body,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new ChangeJobStatus(principal.UserId(), new JobId(id), body.Change), ct)).ToHttp();
}

internal sealed record JobStatusBody(
    JobStatusChange Change
);
