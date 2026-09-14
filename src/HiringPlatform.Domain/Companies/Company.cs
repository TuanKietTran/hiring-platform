using System.Text.RegularExpressions;

namespace HiringPlatform.Domain.Companies;

using HiringPlatform.Domain.Common;

/// <summary>The employer organisation. Plays the role cv-sv's IAM "org" played.</summary>
public sealed partial record Company
{
    private Company()
    {
    }

    public CompanyId Id
    {
        get; private init;
    }
    public string Name { get; private init; } = "";
    public string Slug { get; private init; } = "";
    public Uri? Website
    {
        get; private init;
    }
    public DateTimeOffset CreatedAt
    {
        get; private init;
    }

    public static Company Create(CompanyId id, string name, string? website, DateTimeOffset now)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new DomainException("Company name must not be empty");
        if (trimmed.Length > 120)
            throw new DomainException("Company name must be ≤ 120 characters");

        var slug = NonSlugChars().Replace(trimmed.ToLowerInvariant(), "-").Trim('-');
        if (slug.Length == 0)
            throw new DomainException("Company name must contain letters or digits");

        return new Company
        {
            Id = id,
            Name = trimmed,
            Slug = slug,
            Website = ParseWebsite(website),
            CreatedAt = now.ToUniversalTime(),
        };
    }

    private static Uri? ParseWebsite(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new DomainException("Website must be an absolute http(s) URL");
        return uri;
    }

    public CompanySnapshot ToSnapshot() => new(Id.Value, Name, Slug, Website?.ToString(), CreatedAt);

    public static Company FromSnapshot(CompanySnapshot s) => new()
    {
        Id = new CompanyId(s.Id),
        Name = s.Name,
        Slug = s.Slug,
        Website = s.Website is null ? null : new Uri(s.Website),
        CreatedAt = s.CreatedAt,
    };

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}

public sealed record CompanySnapshot(
    Guid Id,
    string Name,
    string Slug,
    string? Website,
    DateTimeOffset CreatedAt
);
