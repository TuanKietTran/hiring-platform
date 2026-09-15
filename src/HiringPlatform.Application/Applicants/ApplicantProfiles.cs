using HiringPlatform.Application.Common;
using HiringPlatform.Domain.Applicants;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Identity;
using HiringPlatform.Domain.Iam;

namespace HiringPlatform.Application.Applicants;

public sealed record CvDto(
    Guid Id,
    string Name,
    string Url,
    bool IsPrimary,
    DateTimeOffset AddedAt
);
public sealed record ApplicantProfileDto(
    Guid CandidateId,
    string Headline,
    string Summary,
    string Location,
    string? Phone,
    string? PhoneCallingCode,
    IReadOnlyList<string> Skills,
    IReadOnlyList<CvDto> Cvs,
    DateTimeOffset UpdatedAt
)
{
    public static ApplicantProfileDto From(CandidateProfile profile) => new(
        profile.CandidateId.Value, profile.Headline, profile.Summary, profile.Location, profile.Phone?.International,
        profile.Phone?.CallingCode, profile.Skills, [.. profile.Cvs.Select(x => new CvDto(x.Id, x.Name, x.Url.ToString(), x.IsPrimary, x.AddedAt))],
        profile.UpdatedAt);
}

public sealed record GetMyApplicantProfile(
    UserId ActorId
) : IQuery<ApplicantProfileDto>;

public sealed record GetApplicantProfileForApplication(
    UserId ActorId,
    ApplicationId ApplicationId
) : IQuery<ApplicantProfileDto>;

public sealed class GetApplicantProfileForApplicationHandler(
    AccessGuard guard,
    IApplicationRepository applications,
    IJobRepository jobs,
    IApplicantProfileRepository profiles
) : IHandler<GetApplicantProfileForApplication, ApplicantProfileDto>
{
    public async Task<Result<ApplicantProfileDto>> Handle(GetApplicantProfileForApplication request, CancellationToken ct)
    {
        var actor = await guard.Actor(request.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await applications.Find(request.ApplicationId, ct) is not { } application)
            return ApplicationError.NotFound("Application not found");
        var job = await jobs.Find(application.JobId, ct);
        if (job is null)
            return ApplicationError.NotFound("Job not found");
        var resource = new ResourceAttributes(ResourceType.Job, job.Id.Value, null, job.CompanyId);
        if (guard.Check(actor.Value!, resource, AccessAction.ApplicationRead) is { } denied)
            return denied;
        var profile = await profiles.Find(application.CandidateId, ct);
        return profile is null
            ? ApplicationError.NotFound("Applicant profile not found")
            : ApplicantProfileDto.From(profile);
    }
}
public sealed class GetMyApplicantProfileHandler(
    AccessGuard guard,
    IApplicantProfileRepository profiles,
    IClock clock
)
    : IHandler<GetMyApplicantProfile, ApplicantProfileDto>
{
    public async Task<Result<ApplicantProfileDto>> Handle(GetMyApplicantProfile request, CancellationToken ct)
    {
        var actor = await Candidate(guard, request.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        var profile = await profiles.Find(request.ActorId, ct) ?? CandidateProfile.Create(request.ActorId, clock.UtcNow);
        return ApplicantProfileDto.From(profile);
    }

    internal static async Task<Result<User>> Candidate(AccessGuard guard, UserId id, CancellationToken ct)
    {
        var actor = await guard.Actor(id, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        return actor.Value!.Role == Role.Candidate
            ? actor.Value
            : ApplicationError.Forbidden("Only applicants can manage an applicant profile");
    }
}

public sealed record UpdateMyApplicantProfile(
    UserId ActorId,
    string? Headline,
    string? Summary,
    string? Location,
    string? Phone,
    string? PhoneCallingCode,
    IReadOnlyList<string>? Skills
)
    : ICommand<ApplicantProfileDto>;
public sealed class UpdateMyApplicantProfileHandler(
    AccessGuard guard,
    IApplicantProfileRepository profiles,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<UpdateMyApplicantProfile, ApplicantProfileDto>
{
    public async Task<Result<ApplicantProfileDto>> Handle(UpdateMyApplicantProfile request, CancellationToken ct)
    {
        var actor = await GetMyApplicantProfileHandler.Candidate(guard, request.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        var existing = await profiles.Find(request.ActorId, ct);
        var profile = (existing ?? CandidateProfile.Create(request.ActorId, clock.UtcNow)).Update(
            request.Headline, request.Summary, request.Location, request.Phone, request.PhoneCallingCode,
            request.Skills, clock.UtcNow);
        if (existing is null)
            profiles.Add(profile);
        else
            profiles.Update(profile);
        await uow.SaveChanges(ct);
        return ApplicantProfileDto.From(profile);
    }
}

public sealed record AddCv(
    UserId ActorId,
    string Name,
    string Url
) : ICommand<ApplicantProfileDto>;
public sealed class AddCvHandler(
    AccessGuard guard,
    IApplicantProfileRepository profiles,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<AddCv, ApplicantProfileDto>
{
    public async Task<Result<ApplicantProfileDto>> Handle(AddCv request, CancellationToken ct)
    {
        var actor = await GetMyApplicantProfileHandler.Candidate(guard, request.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        var existing = await profiles.Find(request.ActorId, ct);
        var profile = (existing ?? CandidateProfile.Create(request.ActorId, clock.UtcNow))
            .AddCv(Guid.NewGuid(), request.Name, request.Url, clock.UtcNow);
        if (existing is null)
            profiles.Add(profile);
        else
            profiles.Update(profile);
        await uow.SaveChanges(ct);
        return ApplicantProfileDto.From(profile);
    }
}

public sealed record SetPrimaryCv(
    UserId ActorId,
    Guid CvId
) : ICommand<ApplicantProfileDto>;
public sealed class SetPrimaryCvHandler(
    AccessGuard guard,
    IApplicantProfileRepository profiles,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<SetPrimaryCv, ApplicantProfileDto>
{
    public async Task<Result<ApplicantProfileDto>> Handle(SetPrimaryCv request, CancellationToken ct)
    {
        var actor = await GetMyApplicantProfileHandler.Candidate(guard, request.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await profiles.Find(request.ActorId, ct) is not { } profile)
            return ApplicationError.NotFound("Applicant profile not found");
        profile = profile.SetPrimaryCv(request.CvId, clock.UtcNow);
        profiles.Update(profile);
        await uow.SaveChanges(ct);
        return ApplicantProfileDto.From(profile);
    }
}

public sealed record RemoveCv(
    UserId ActorId,
    Guid CvId
) : ICommand<ApplicantProfileDto>;
public sealed class RemoveCvHandler(
    AccessGuard guard,
    IApplicantProfileRepository profiles,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<RemoveCv, ApplicantProfileDto>
{
    public async Task<Result<ApplicantProfileDto>> Handle(RemoveCv request, CancellationToken ct)
    {
        var actor = await GetMyApplicantProfileHandler.Candidate(guard, request.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (await profiles.Find(request.ActorId, ct) is not { } profile)
            return ApplicationError.NotFound("Applicant profile not found");
        profile = profile.RemoveCv(request.CvId, clock.UtcNow);
        profiles.Update(profile);
        await uow.SaveChanges(ct);
        return ApplicantProfileDto.From(profile);
    }
}
