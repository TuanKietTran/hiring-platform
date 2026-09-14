using System.Text.RegularExpressions;

namespace HiringPlatform.Domain.Common;

/// <summary>Canonical (trimmed, lower-cased) email address. Ported from cv-sv iam/email.ts.</summary>
public sealed partial record Email
{
    private Email(string value) => Value = value;

    public string Value
    {
        get;
    }

    public static Email Create(string? raw)
    {
        var trimmed = raw?.Trim().ToLowerInvariant() ?? "";
        if (trimmed.Length == 0)
            throw new DomainException("Empty email address");
        if (trimmed.Length > 254 || !Pattern().IsMatch(trimmed))
            throw new DomainException("Invalid email address");
        return new Email(trimmed);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex Pattern();
}
