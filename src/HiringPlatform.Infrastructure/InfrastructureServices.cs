using System.Globalization;
using System.Security.Cryptography;

using HiringPlatform.Application.Common;
using HiringPlatform.Domain.Identity;
using HiringPlatform.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPlatform.Infrastructure;

public static class InfrastructureServices
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("hiringdb")
            ?? throw new InvalidOperationException("ConnectionStrings:hiringdb is required");
        services.AddDbContext<HiringDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IInterviewRepository, InterviewRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }

    /// <summary>Creates the prototype schema. Replace with EF migrations before production deployment.</summary>
    public static async Task EnsureDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HiringDbContext>().Database.EnsureCreatedAsync();
    }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>PBKDF2-SHA256 with a random 128-bit salt; constant-time verification.</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public HashedPassword Hash(PlainPassword plain)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(plain.Value, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return HashedPassword.FromHash($"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}");
    }

    public bool Verify(PlainPassword plain, HashedPassword hashed)
    {
        try
        {
            var parts = hashed.Hash.Split('$');
            if (parts is not ["pbkdf2-sha256", var count, var saltText, var expectedText])
                return false;
            var salt = Convert.FromBase64String(saltText);
            var expected = Convert.FromBase64String(expectedText);
            var actual = Rfc2898DeriveBytes.Pbkdf2(plain.Value, salt, int.Parse(count, CultureInfo.InvariantCulture), HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
        catch (OverflowException) { return false; }
    }
}
