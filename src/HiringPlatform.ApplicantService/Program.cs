using System.Text.Json;
using System.Text.Json.Serialization;

using HiringPlatform.Api;
using HiringPlatform.Application.Common;
using HiringPlatform.Infrastructure;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
ConfigureSharedAuthentication(builder);

var app = builder.Build();
ConfigurePipeline(app);
var api = app.MapGroup(ApiConstants.ApiPrefix);
api.MapPublicJobEndpoints();
api.MapApplicantApplicationEndpoints();
api.MapApplicantProfileEndpoints();
app.Run();

static void ConfigureSharedAuthentication(WebApplicationBuilder builder)
{
    var keyPath = builder.Configuration[ApiConstants.SharedAuthKeyPathSetting] ?? Path.Combine(Path.GetTempPath(), ApiConstants.DefaultKeyDirectory);
    Directory.CreateDirectory(keyPath);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath)).SetApplicationName(ApiConstants.SharedAuthApplicationName);
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
    {
        o.Cookie.Name = ApiConstants.CookieName;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.Cookie.Path = "/";
        o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    });
    builder.Services.AddAuthorization();
}

static void ConfigurePipeline(WebApplication app)
{
    app.UseExceptionHandler(error => error.Run(async context =>
    {
        context.Response.StatusCode = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error is UnauthorizedAccessException ? 401 : 500;
        await context.Response.WriteAsJsonAsync(new
        {
            error = context.Response.StatusCode == 500 ? ApiConstants.UnexpectedError : ApiConstants.UnauthorizedError
        });
    }));
    app.UseAuthentication();
    app.UseAuthorization();
    if (app.Environment.IsDevelopment())
        app.MapOpenApi();
    app.MapDefaultEndpoints();
}
