using System.Security.Claims;

using HiringPlatform.Application.Applicants;
using HiringPlatform.Application.Common;

namespace HiringPlatform.Api;

internal static class ApplicantProfileEndpoints
{
    public static RouteGroupBuilder MapApplicantProfileEndpoints(this RouteGroupBuilder api)
    {
        var profile = api.MapGroup("/applicant/profile").RequireAuthorization();
        profile.MapGet("", GetProfile);
        profile.MapPut("", UpdateProfile);
        profile.MapPost("/cvs", AddCv);
        profile.MapPost("/cvs/{cvId:guid}/primary", SetPrimaryCv);
        profile.MapDelete("/cvs/{cvId:guid}", RemoveCv);
        return api;
    }

    private static async Task<IResult> GetProfile(ClaimsPrincipal principal, Mediator mediator, CancellationToken ct) =>
        (await mediator.Send(new GetMyApplicantProfile(principal.UserId()), ct)).ToHttp();

    private static async Task<IResult> UpdateProfile(
        UpdateProfileBody body, ClaimsPrincipal principal, Mediator mediator, CancellationToken ct) =>
        (await mediator.Send(new UpdateMyApplicantProfile(
            principal.UserId(), body.Headline, body.Summary, body.Location, body.Phone, body.PhoneCallingCode, body.Skills), ct)).ToHttp();

    private static async Task<IResult> AddCv(
        AddCvBody body, ClaimsPrincipal principal, Mediator mediator, CancellationToken ct) =>
        (await mediator.Send(new AddCv(principal.UserId(), body.Name, body.Url), ct)).ToHttp();

    private static async Task<IResult> SetPrimaryCv(
        Guid cvId, ClaimsPrincipal principal, Mediator mediator, CancellationToken ct) =>
        (await mediator.Send(new SetPrimaryCv(principal.UserId(), cvId), ct)).ToHttp();

    private static async Task<IResult> RemoveCv(
        Guid cvId, ClaimsPrincipal principal, Mediator mediator, CancellationToken ct) =>
        (await mediator.Send(new RemoveCv(principal.UserId(), cvId), ct)).ToHttp();
}

internal sealed record UpdateProfileBody(
    string? Headline,
    string? Summary,
    string? Location,
    string? Phone,
    string? PhoneCallingCode,
    IReadOnlyList<string>? Skills
);
internal sealed record AddCvBody(
    string Name,
    string Url
);
