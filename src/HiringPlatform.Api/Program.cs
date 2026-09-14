using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

using HiringPlatform.Api;
using HiringPlatform.Application.Applications;
using HiringPlatform.Application.Common;
using HiringPlatform.Application.Identity;
using HiringPlatform.Application.Interviews;
using HiringPlatform.Application.Jobs;
using HiringPlatform.Domain.Applications;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Identity;
using HiringPlatform.Domain.Interviews;
using HiringPlatform.Domain.Jobs;
using HiringPlatform.Infrastructure;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "hiring.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();

var app = builder.Build();
await app.Services.EnsureDatabaseAsync();
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error
        is UnauthorizedAccessException ? 401 : 500;
    await context.Response.WriteAsJsonAsync(new
    {
        error = context.Response.StatusCode == 500 ? "Unexpected server error" : "Unauthorized"
    });
}));
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapDefaultEndpoints();

var api = app.MapGroup("/api");

// Public identity endpoints
api.MapPost("/auth/register/candidate", async (RegisterCandidateBody body, Mediator mediator, HttpContext http, CancellationToken ct) =>
{
    var result = await mediator.Send(new RegisterCandidate(body.Email, body.Password, body.FullName), ct);
    if (result.IsSuccess)
        await SignIn(http, result.Value!);
    return result.ToHttp();
});
api.MapPost("/auth/register/company", async (RegisterCompanyBody body, Mediator mediator, HttpContext http, CancellationToken ct) =>
{
    var result = await mediator.Send(new RegisterCompany(body.CompanyName, body.Website, body.Email, body.Password, body.FullName), ct);
    if (result.IsSuccess)
        await SignIn(http, result.Value!);
    return result.ToHttp();
});
api.MapPost("/auth/login", async (LoginBody body, Mediator mediator, HttpContext http, CancellationToken ct) =>
{
    var result = await mediator.Send(new Login(body.Email, body.Password), ct);
    if (result.IsSuccess)
        await SignIn(http, result.Value!);
    return result.ToHttp();
});
api.MapPost("/auth/logout", async (HttpContext http) => { await http.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
api.MapGet("/auth/me", async (ClaimsPrincipal principal, Mediator mediator, CancellationToken ct) =>
    (await mediator.Send(new GetUser(principal.UserId()), ct)).ToHttp()).RequireAuthorization();

// Public job board
api.MapGet("/jobs", async (string? q, WorkplaceType? workplace, int page, int pageSize, Mediator mediator, CancellationToken ct) =>
    (await mediator.Send(new SearchJobs(q, workplace, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct)).ToHttp());
api.MapGet("/jobs/{id:guid}", async (Guid id, ClaimsPrincipal principal, Mediator mediator, CancellationToken ct) =>
{
    UserId? actor = principal.Identity?.IsAuthenticated == true ? principal.UserId() : null;
    return (await mediator.Send(new GetJob(actor, new JobId(id)), ct)).ToHttp();
});

var secured = api.MapGroup("").RequireAuthorization();

// Hiring team
secured.MapGet("/staff", async (ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new ListStaff(p.UserId()), ct)).ToHttp());
secured.MapPost("/staff", async (AddStaffBody b, ClaimsPrincipal p, Mediator m, CancellationToken ct) =>
    (await m.Send(new AddStaffMember(p.UserId(), b.Email, b.Password, b.FullName, b.Role), ct)).ToHttp());
secured.MapGet("/company/jobs", async (ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new ListCompanyJobs(p.UserId()), ct)).ToHttp());
secured.MapPost("/jobs", async (JobInput b, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new CreateJob(p.UserId(), b), ct)).ToHttp());
secured.MapPut("/jobs/{id:guid}", async (Guid id, JobInput b, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new UpdateJob(p.UserId(), new JobId(id), b), ct)).ToHttp());
secured.MapPost("/jobs/{id:guid}/status", async (Guid id, JobStatusBody b, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new ChangeJobStatus(p.UserId(), new JobId(id), b.Change), ct)).ToHttp());

// Applications
secured.MapPost("/jobs/{id:guid}/applications", async (Guid id, ApplyBody b, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new ApplyToJob(p.UserId(), new JobId(id), b.CoverLetter, b.ResumeUrl), ct)).ToHttp());
secured.MapGet("/applications/mine", async (ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new ListMyApplications(p.UserId()), ct)).ToHttp());
secured.MapGet("/jobs/{id:guid}/applications", async (Guid id, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new ListJobApplications(p.UserId(), new JobId(id)), ct)).ToHttp());
secured.MapGet("/applications/{id:guid}", async (Guid id, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new GetApplication(p.UserId(), new ApplicationId(id)), ct)).ToHttp());
secured.MapPost("/applications/{id:guid}/advance", async (Guid id, AdvanceBody b, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new AdvanceApplication(p.UserId(), new ApplicationId(id), b.To, b.Note), ct)).ToHttp());
secured.MapPost("/applications/{id:guid}/withdraw", async (Guid id, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new WithdrawApplication(p.UserId(), new ApplicationId(id)), ct)).ToHttp());

// Interviews
secured.MapGet("/applications/{id:guid}/interviews", async (Guid id, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new ListApplicationInterviews(p.UserId(), new ApplicationId(id)), ct)).ToHttp());
secured.MapPost("/applications/{id:guid}/interviews", async (Guid id, ScheduleBody b, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new ScheduleInterview(p.UserId(), new ApplicationId(id), b.Kind, b.StartsAt, b.DurationMinutes, b.InterviewerIds), ct)).ToHttp());
secured.MapPost("/interviews/{id:guid}/cancel", async (Guid id, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new CancelInterview(p.UserId(), new InterviewId(id)), ct)).ToHttp());
secured.MapPost("/interviews/{id:guid}/feedback", async (Guid id, FeedbackBody b, ClaimsPrincipal p, Mediator m, CancellationToken ct) => (await m.Send(new SubmitFeedback(p.UserId(), new InterviewId(id), b.Recommendation, b.Notes), ct)).ToHttp());

app.Run();

static Task SignIn(HttpContext http, UserDto user)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.FullName),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
    };
    return http.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
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
internal sealed record JobStatusBody(
    JobStatusChange Change
);
internal sealed record ApplyBody(
    string? CoverLetter,
    string? ResumeUrl
);
internal sealed record AdvanceBody(
    ApplicationStage To,
    string? Note
);
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

#pragma warning disable CA1050 // WebApplicationFactory discovers the top-level Program type globally.
public partial class Program;
#pragma warning restore CA1050
