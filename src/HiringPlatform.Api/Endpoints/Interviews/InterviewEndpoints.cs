using System.Security.Claims;

using HiringPlatform.Application.Common;
using HiringPlatform.Application.Interviews;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Interviews;

namespace HiringPlatform.Api;

internal static class InterviewEndpoints
{
    public static RouteGroupBuilder MapInterviewEndpoints(this RouteGroupBuilder api)
    {
        var secured = api.MapGroup("").RequireAuthorization();
        secured.MapGet("/applications/{id:guid}/interviews", ListApplicationInterviews);
        secured.MapPost("/applications/{id:guid}/interviews", ScheduleInterview);
        secured.MapPost("/interviews/{id:guid}/cancel", CancelInterview);
        secured.MapPost("/interviews/{id:guid}/feedback", SubmitFeedback);

        return api;
    }

    private static async Task<IResult> ListApplicationInterviews(
        Guid id,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(
            new ListApplicationInterviews(principal.UserId(), new ApplicationId(id)), ct)).ToHttp();

    private static async Task<IResult> ScheduleInterview(
        Guid id,
        ScheduleBody body,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new ScheduleInterview(
            principal.UserId(),
            new ApplicationId(id),
            body.Kind,
            body.StartsAt,
            body.DurationMinutes,
            body.InterviewerIds), ct)).ToHttp();

    private static async Task<IResult> CancelInterview(
        Guid id,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new CancelInterview(principal.UserId(), new InterviewId(id)), ct)).ToHttp();

    private static async Task<IResult> SubmitFeedback(
        Guid id,
        FeedbackBody body,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(
            new SubmitFeedback(principal.UserId(), new InterviewId(id), body.Recommendation, body.Notes), ct)).ToHttp();
}

internal sealed record ScheduleBody(
    InterviewKind Kind,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    Guid[] InterviewerIds
);
internal sealed record FeedbackBody(
    Recommendation Recommendation,
    string? Notes
);
