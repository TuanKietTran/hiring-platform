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

const string SharedKeyPath = "/tmp/hirelane-auth-keys";
const string MinioAccessKey = "hirelane-local";
const string MinioSecretKey = "hirelane-local-secret";
const int ServiceReplicas = 2;

var minio = builder.AddContainer("minio", "quay.io/minio/minio")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEnvironment("MINIO_ROOT_USER", MinioAccessKey)
    .WithEnvironment("MINIO_ROOT_PASSWORD", MinioSecretKey)
    .WithVolume("hiring-minio-data", "/data")
    .WithHttpEndpoint(targetPort: 9000, name: "api")
    .WithHttpEndpoint(targetPort: 9001, name: "console");

var identity = builder.AddProject<Projects.HiringPlatform_IdentityService>("identity-service")
    .WithReference(database)
    .WaitForCompletion(migrator)
    .WithEnvironment("SharedAuth__KeyPath", SharedKeyPath)
    .WithHttpHealthCheck("/health")
    .WithReplicas(ServiceReplicas);

var applicant = builder.AddProject<Projects.HiringPlatform_ApplicantService>("applicant-service")
    .WithReference(database)
    .WaitForCompletion(migrator)
    .WithEnvironment("SharedAuth__KeyPath", SharedKeyPath)
    .WithHttpHealthCheck("/health")
    .WithReplicas(ServiceReplicas);

var recruiter = builder.AddProject<Projects.HiringPlatform_RecruiterService>("recruiter-service")
    .WithReference(database)
    .WaitForCompletion(migrator)
    .WithEnvironment("SharedAuth__KeyPath", SharedKeyPath)
    .WithHttpHealthCheck("/health")
    .WithReplicas(ServiceReplicas);

var blobs = builder.AddProject<Projects.HiringPlatform_BlobService>("blob-service")
    .WaitFor(minio)
    .WithEnvironment("S3__Endpoint", minio.GetEndpoint("api"))
    .WithEnvironment("S3__AccessKey", MinioAccessKey)
    .WithEnvironment("S3__SecretKey", MinioSecretKey)
    .WithEnvironment("SharedAuth__KeyPath", SharedKeyPath)
    .WithHttpHealthCheck("/health")
    .WithReplicas(ServiceReplicas);

// The gateway is the sole browser-facing API. Aspire service discovery load-balances
// each request over the healthy replicas of the selected bounded-context service.
var gateway = builder.AddProject<Projects.HiringPlatform_Gateway>("gateway")
    .WithReference(identity)
    .WithReference(applicant)
    .WithReference(recruiter)
    .WithReference(blobs)
    .WaitFor(identity)
    .WaitFor(applicant)
    .WaitFor(recruiter)
    .WaitFor(blobs)
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("web", "../hiring-web", "start")
    .WithNpm(install: true)
    .WithReference(gateway)
    .WaitFor(gateway)
    .WithHttpEndpoint(port: 4201, targetPort: 4200, name: "http", env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
