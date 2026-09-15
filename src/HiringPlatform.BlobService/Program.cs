using HiringPlatform.Api;
using HiringPlatform.BlobService;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

using Minio;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IMinioClient>(_ =>
{
    var endpoint = new Uri(builder.Configuration["S3:Endpoint"]
        ?? throw new InvalidOperationException("S3:Endpoint is required"));
    var client = new MinioClient()
        .WithEndpoint(endpoint.Host, endpoint.Port)
        .WithCredentials(
            builder.Configuration["S3:AccessKey"] ?? throw new InvalidOperationException("S3:AccessKey is required"),
            builder.Configuration["S3:SecretKey"] ?? throw new InvalidOperationException("S3:SecretKey is required"));
    if (endpoint.Scheme == Uri.UriSchemeHttps)
        client.WithSSL();
    return client.Build();
});
builder.Services.AddSingleton<BlobStorage>();
ConfigureSharedAuthentication(builder);

var app = builder.Build();
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error
        is UnauthorizedAccessException ? 401 : 500;
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
app.MapGroup(ApiConstants.ApiPrefix).MapBlobEndpoints();
app.Run();

static void ConfigureSharedAuthentication(WebApplicationBuilder builder)
{
    var keyPath = builder.Configuration[ApiConstants.SharedAuthKeyPathSetting]
        ?? Path.Combine(Path.GetTempPath(), ApiConstants.DefaultKeyDirectory);
    Directory.CreateDirectory(keyPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
        .SetApplicationName(ApiConstants.SharedAuthApplicationName);
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
    {
        options.Cookie.Name = ApiConstants.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Path = "/";
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    });
    builder.Services.AddAuthorization();
}
