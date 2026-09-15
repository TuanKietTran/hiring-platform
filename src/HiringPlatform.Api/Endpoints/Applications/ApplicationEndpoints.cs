using System.Security.Claims;

using HiringPlatform.Application.Applications;
using HiringPlatform.Application.Common;
using HiringPlatform.Domain.Applications;
using HiringPlatform.Domain.Common;

namespace HiringPlatform.Api;

internal static class ApplicationEndpoints
{
    public static RouteGroupBuilder MapApplicationEndpoints(this RouteGroupBuilder api) =>
        api.MapApplicantApplicationEndpoints().MapRecruiterApplicationEndpoints();

    public static RouteGroupBuilder MapApplicantApplicationEndpoints(this RouteGroupBuilder api)
    {
        var secured = api.MapGroup("").RequireAuthorization();
        secured.MapPost("/jobs/{id:guid}/applications", ApplyToJob);
        secured.MapGet("/applications/mine", ListMyApplications);
        secured.MapPost("/applications/{id:guid}/withdraw", WithdrawApplication);
        return api;
    }

    public static RouteGroupBuilder MapRecruiterApplicationEndpoints(this RouteGroupBuilder api)
    {
        var secured = api.MapGroup("").RequireAuthorization();
        secured.MapGet("/jobs/{id:guid}/applications", ListJobApplications);
        secured.MapGet("/applications/{id:guid}", GetApplication);
        secured.MapPost("/applications/{id:guid}/advance", AdvanceApplication);
        return api;
    }

    private static async Task<IResult> ApplyToJob(
        Guid id,
        ApplyBody body,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(
            new ApplyToJob(principal.UserId(), new JobId(id), body.CoverLetter, body.ResumeUrl), ct)).ToHttp();

    private static async Task<IResult> ListMyApplications(
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new ListMyApplications(principal.UserId()), ct)).ToHttp();

    private static async Task<IResult> ListJobApplications(
        Guid id,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new ListJobApplications(principal.UserId(), new JobId(id)), ct)).ToHttp();

    private static async Task<IResult> GetApplication(
        Guid id,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new GetApplication(principal.UserId(), new ApplicationId(id)), ct)).ToHttp();

    private static async Task<IResult> AdvanceApplication(
        Guid id,
        AdvanceBody body,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(
            new AdvanceApplication(principal.UserId(), new ApplicationId(id), body.To, body.Note), ct)).ToHttp();

    private static async Task<IResult> WithdrawApplication(
        Guid id,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new WithdrawApplication(principal.UserId(), new ApplicationId(id)), ct)).ToHttp();
}

internal sealed record ApplyBody(
    string? CoverLetter,
    string? ResumeUrl
);
internal sealed record AdvanceBody(
    ApplicationStage To,
    string? Note
);
