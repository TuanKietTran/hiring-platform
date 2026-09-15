using System.Text.Json;
using System.Text.Json.Serialization;

using HiringPlatform.Api;
using HiringPlatform.Application.Common;
using HiringPlatform.Infrastructure;

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
    o.Cookie.Name = ApiConstants.CookieName;
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();

var app = builder.Build();

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
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapDefaultEndpoints();

var api = app.MapGroup("/api");
api.MapIdentityEndpoints();
api.MapAccessEndpoints();
api.MapJobEndpoints();
api.MapApplicationEndpoints();
api.MapApplicantProfileEndpoints();
api.MapInterviewEndpoints();

app.Run();
