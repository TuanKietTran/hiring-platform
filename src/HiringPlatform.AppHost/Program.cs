using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// In the OrbStack kube dev pod, PostgreSQL is a restart-safe Kubernetes service and
// the connection string is injected into AppHost. Outside Kubernetes, Aspire manages it.
IResourceBuilder<IResourceWithConnectionString> database;
if (builder.Configuration.GetConnectionString("hiringdb") is not null)
{
    database = builder.AddConnectionString("hiringdb");
}
else
{
    database = builder.AddPostgres("postgres")
        .WithDataVolume("hiring-postgres-data")
        .WithPgAdmin()
        .AddDatabase("hiringdb");
}

var migrator = builder.AddProject<Projects.HiringPlatform_Migrator>("migrator")
    .WithReference(database)
    .WaitFor(database);

if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
    migrator.WithEnvironment("DevelopmentSeed__Enabled", "true");

var api = builder.AddProject<Projects.HiringPlatform_Api>("api")
    .WithReference(database)
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("web", "../hiring-web", "start")
    .WithNpm(install: true)
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 4201, targetPort: 4200, name: "http", env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
