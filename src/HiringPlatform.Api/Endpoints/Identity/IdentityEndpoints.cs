using System.Security.Claims;

using HiringPlatform.Application.Common;
using HiringPlatform.Application.Identity;
using HiringPlatform.Domain.Identity;

namespace HiringPlatform.Api;

internal static class IdentityEndpoints
{
    public static RouteGroupBuilder MapIdentityEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/register/candidate", RegisterCandidate);
        api.MapPost("/auth/register/company", RegisterCompany);
        api.MapPost("/auth/login", Login);

        var secured = api.MapGroup("").RequireAuthorization();
        secured.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutUser();
            return Results.NoContent();
        });
        secured.MapGet("/auth/me", GetCurrentUser);
        secured.MapGet("/staff", ListStaff);
        secured.MapPost("/staff", AddStaff);

        return api;
    }

    private static async Task<IResult> RegisterCandidate(
        RegisterCandidateBody body,
        Mediator mediator,
        HttpContext http,
        CancellationToken ct)
    {
        var result = await mediator.Send(new RegisterCandidate(body.Email, body.Password, body.FullName), ct);
        if (result.IsSuccess)
            await http.SignInUser(result.Value!);
        return result.ToHttp();
    }

    private static async Task<IResult> RegisterCompany(
        RegisterCompanyBody body,
        Mediator mediator,
        HttpContext http,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new RegisterCompany(body.CompanyName, body.Website, body.Email, body.Password, body.FullName), ct);
        if (result.IsSuccess)
            await http.SignInUser(result.Value!);
        return result.ToHttp();
    }

    private static async Task<IResult> Login(
        LoginBody body,
        Mediator mediator,
        HttpContext http,
        CancellationToken ct)
    {
        var result = await mediator.Send(new Login(body.Email, body.Password), ct);
        if (result.IsSuccess)
            await http.SignInUser(result.Value!);
        return result.ToHttp();
    }

    private static async Task<IResult> GetCurrentUser(
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new GetUser(principal.UserId()), ct)).ToHttp();

    private static async Task<IResult> ListStaff(
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new ListStaff(principal.UserId()), ct)).ToHttp();

    private static async Task<IResult> AddStaff(
        AddStaffBody body,
        ClaimsPrincipal principal,
        Mediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(
            new AddStaffMember(principal.UserId(), body.Email, body.Password, body.FullName, body.Role), ct)).ToHttp();
}

internal sealed record RegisterCandidateBody(
    string Email,
    string Password,
    string FullName
);
internal sealed record RegisterCompanyBody(
    string CompanyName,
    string? Website,
    string Email,
    string Password,
    string FullName
);
internal sealed record LoginBody(
    string Email,
    string Password
);
internal sealed record AddStaffBody(
    string Email,
    string Password,
    string FullName,
    Role Role
);
