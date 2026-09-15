using HiringPlatform.Application.Common;
using HiringPlatform.Application.Identity;
using HiringPlatform.Domain.Common;

using Microsoft.Extensions.DependencyInjection;

namespace HiringPlatform.Migrator;

internal static class DevelopmentDataSeeder
{
    private const string Password = "HirelaneDev1!";
    private const string CandidateEmail = "candidate@hirelane.dev";
    private const string AdminEmail = "admin@hirelane.dev";

    public static async Task SeedDevelopmentData(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<Mediator>();

        if (await users.FindByEmail(Email.Create(CandidateEmail), ct) is null)
            await EnsureSuccess(
                mediator.Send(new RegisterCandidate(CandidateEmail, Password, "Dev Candidate"), ct),
                CandidateEmail);

        if (await users.FindByEmail(Email.Create(AdminEmail), ct) is null)
            await EnsureSuccess(
                mediator.Send(new RegisterCompany(
                    "Hirelane Dev Company",
                    "https://hirelane.dev",
                    AdminEmail,
                    Password,
                    "Dev Org Admin"), ct),
                AdminEmail);
    }

    private static async Task EnsureSuccess(Task<Result<UserDto>> pending, string email)
    {
        var result = await pending;
        if (!result.IsSuccess)
            throw new InvalidOperationException($"Could not seed development account '{email}': {result.Error!.Message}");
    }
}
