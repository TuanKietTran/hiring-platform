using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

namespace Hirelane.ServiceClients;

/// <summary>Stable route constants shared by typed clients. Consumers should not construct internal URLs.</summary>
public static class ServiceRoutes
{
    public const string Me = "/api/auth/me";
    public const string EvaluateAccess = "/api/access/evaluate";
    public const string ApplicantProfile = "/api/applicant/profile";
    public const string MyApplications = "/api/applications/mine";
    public static string JobApplications(Guid jobId) => $"/api/jobs/{jobId}/applications";
}

public sealed record PublicUser(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    Guid? CompanyId,
    string? CompanyName
);
public sealed record AccessEvaluationRequest(
    string ResourceType,
    Guid ResourceId,
    Guid? OwnerUserId,
    Guid? OwnerCompanyId,
    bool IsPublic,
    string Action
);
public sealed record AccessEvaluation(
    bool Allowed,
    string Effect,
    string Reason
);
public sealed record PublicCv(
    Guid Id,
    string Name,
    string Url,
    bool IsPrimary,
    DateTimeOffset AddedAt
);
public sealed record PublicApplicantProfile(
    Guid CandidateId,
    string Headline,
    string Summary,
    string Location,
    string? Phone,
    string? PhoneCallingCode,
    IReadOnlyList<string> Skills,
    IReadOnlyList<PublicCv> Cvs,
    DateTimeOffset UpdatedAt
);

public interface IIdentityServiceClient
{
    Task<PublicUser?> GetCurrentUser(CancellationToken ct = default);
    Task<AccessEvaluation> Evaluate(AccessEvaluationRequest request, CancellationToken ct = default);
}

public interface IApplicantServiceClient
{
    Task<PublicApplicantProfile> GetMyProfile(CancellationToken ct = default);
}

public interface IRecruiterServiceClient
{
    Task<T[]> GetJobApplications<T>(Guid jobId, CancellationToken ct = default);
}

internal sealed class IdentityServiceClient(
    HttpClient http
) : IIdentityServiceClient
{
    public Task<PublicUser?> GetCurrentUser(CancellationToken ct = default) =>
        http.GetFromJsonAsync<PublicUser>(ServiceRoutes.Me, ct);
    public async Task<AccessEvaluation> Evaluate(AccessEvaluationRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(ServiceRoutes.EvaluateAccess, request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccessEvaluation>(cancellationToken: ct))!;
    }
}

internal sealed class ApplicantServiceClient(
    HttpClient http
) : IApplicantServiceClient
{
    public async Task<PublicApplicantProfile> GetMyProfile(CancellationToken ct = default) =>
        (await http.GetFromJsonAsync<PublicApplicantProfile>(ServiceRoutes.ApplicantProfile, ct))!;
}

internal sealed class RecruiterServiceClient(
    HttpClient http
) : IRecruiterServiceClient
{
    public async Task<T[]> GetJobApplications<T>(Guid jobId, CancellationToken ct = default) =>
        (await http.GetFromJsonAsync<T[]>(ServiceRoutes.JobApplications(jobId), ct)) ?? [];
}

public static class ServiceClientRegistration
{
    public static IServiceCollection AddHirelaneServiceClients(
        this IServiceCollection services, Uri identityBaseAddress, Uri applicantBaseAddress, Uri recruiterBaseAddress)
    {
        services.AddHttpClient<IIdentityServiceClient, IdentityServiceClient>(x => x.BaseAddress = identityBaseAddress);
        services.AddHttpClient<IApplicantServiceClient, ApplicantServiceClient>(x => x.BaseAddress = applicantBaseAddress);
        services.AddHttpClient<IRecruiterServiceClient, RecruiterServiceClient>(x => x.BaseAddress = recruiterBaseAddress);
        return services;
    }
}
