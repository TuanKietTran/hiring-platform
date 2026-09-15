using HiringPlatform.Application.Common;
using HiringPlatform.Infrastructure;
using HiringPlatform.Migrator;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

using var host = builder.Build();
await host.Services.EnsureDatabaseAsync();

var environment = host.Services.GetRequiredService<IHostEnvironment>();
if (environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
    await host.Services.SeedDevelopmentData();
