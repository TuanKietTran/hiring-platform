using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services
    .AddReverseProxy()
    .LoadFromMemory(GatewayRoutes.All, GatewayRoutes.Clusters)
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapReverseProxy();
app.Run();

internal static class GatewayRoutes
{
    public static IReadOnlyList<RouteConfig> All { get; } =
    [
        Prefix("identity-auth", GatewayConstants.IdentityClient, "/api/auth/{**rest}"),
        Prefix("identity-staff", GatewayConstants.IdentityClient, "/api/staff/{**rest}"),
        Prefix("identity-access", GatewayConstants.IdentityClient, "/api/access/{**rest}"),

        Prefix("blobs", GatewayConstants.BlobClient, "/api/blobs/{**rest}"),

        Prefix("applicant-profile", GatewayConstants.ApplicantClient, "/api/applicant/{**rest}"),
        Exact("applicant-mine", GatewayConstants.ApplicantClient, "/api/applications/mine", "GET"),
        Exact("applicant-apply", GatewayConstants.ApplicantClient, "/api/jobs/{id}/applications", "POST"),
        Exact("applicant-withdraw", GatewayConstants.ApplicantClient, "/api/applications/{id}/withdraw", "POST"),
        Exact("applicant-job-search", GatewayConstants.ApplicantClient, "/api/jobs", "GET"),
        Exact("applicant-job-detail", GatewayConstants.ApplicantClient, "/api/jobs/{id}", "GET"),

        Prefix("recruiter-company", GatewayConstants.RecruiterClient, "/api/company/{**rest}"),
        Exact("recruiter-create-job", GatewayConstants.RecruiterClient, "/api/jobs", "POST"),
        Exact("recruiter-update-job", GatewayConstants.RecruiterClient, "/api/jobs/{id}", "PUT"),
        Exact("recruiter-job-status", GatewayConstants.RecruiterClient, "/api/jobs/{id}/status", "POST"),
        Exact("recruiter-job-applications", GatewayConstants.RecruiterClient, "/api/jobs/{id}/applications", "GET"),
        Exact("recruiter-application", GatewayConstants.RecruiterClient, "/api/applications/{id}", "GET"),
        Exact("recruiter-advance", GatewayConstants.RecruiterClient, "/api/applications/{id}/advance", "POST"),
        Exact("recruiter-interviews", GatewayConstants.RecruiterClient, "/api/applications/{id}/interviews", "GET", "POST"),
        Exact("recruiter-cancel-interview", GatewayConstants.RecruiterClient, "/api/interviews/{id}/cancel", "POST"),
        Exact("recruiter-interview-feedback", GatewayConstants.RecruiterClient, "/api/interviews/{id}/feedback", "POST"),
    ];

    public static IReadOnlyList<ClusterConfig> Clusters { get; } =
    [
        Cluster(GatewayConstants.IdentityClient, GatewayConstants.IdentityAddress),
        Cluster(GatewayConstants.ApplicantClient, GatewayConstants.ApplicantAddress),
        Cluster(GatewayConstants.BlobClient, GatewayConstants.BlobAddress),
        Cluster(GatewayConstants.RecruiterClient, GatewayConstants.RecruiterAddress),
    ];

    private static RouteConfig Prefix(string id, string cluster, string path) =>
        new() { RouteId = id, ClusterId = cluster, Match = new RouteMatch { Path = path } };

    private static RouteConfig Exact(string id, string cluster, string path, params string[] methods) =>
        new() { RouteId = id, ClusterId = cluster, Match = new RouteMatch { Path = path, Methods = methods } };

    private static ClusterConfig Cluster(string id, string address) => new()
    {
        ClusterId = id,
        Destinations = new Dictionary<string, DestinationConfig>
        {
            [id] = new() { Address = address },
        },
    };
}
