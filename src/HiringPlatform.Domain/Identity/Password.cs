namespace HiringPlatform.Domain.Identity;

using HiringPlatform.Domain.Common;

// Ported from cv-sv iam/password.ts: plaintext and hashes are distinct types and never print.

public sealed record PlainPassword
{
    private PlainPassword(string value) => Value = value;

    public string Value
    {
        get;
    }

    public static PlainPassword Create(string? raw, PasswordValidator validator)
    {
        var failures = validator.Validate(raw ?? "");
        if (failures.Count > 0)
            throw new WeakPasswordException(failures);
        return new PlainPassword(raw!);
    }

    // for verifying existing credentials: strength rules don't apply
    public static PlainPassword Unchecked(string? raw) => new(raw ?? "");

    public override string ToString() => "[REDACTED]";
}

public sealed record HashedPassword
{
    private HashedPassword(string hash) => Hash = hash;

    public string Hash
    {
        get;
    }

    public static HashedPassword FromHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
            throw new DomainException("hash must be provided");
        return new HashedPassword(hash);
    }

    public override string ToString() => "[REDACTED]";
}

public interface IPasswordHasher
{
    HashedPassword Hash(PlainPassword plain);
    bool Verify(PlainPassword plain, HashedPassword hashed);
}

public sealed record PasswordFailure(
    string Code,
    string Message
);

public sealed class WeakPasswordException(
    IReadOnlyList<PasswordFailure> failures
)
    : DomainException("password invalid: " + string.Join(", ", failures.Select(f => $"{f.Code}: {f.Message}")))
{
    public IReadOnlyList<PasswordFailure> Failures { get; } = failures;
}

public interface IPasswordRule
{
    string Code
    {
        get;
    }
    string? Validate(string raw);
}

/// <summary>Composes rules and reports every failure, not just the first.</summary>
public sealed class PasswordValidator(
    IReadOnlyList<IPasswordRule> rules
)
{
    public static PasswordValidator Default
    {
        get;
    } = new(
    [
        new MinLengthRule(10),
        new HasUppercaseRule(),
        new HasDigitRule(),
        new NotCommonRule(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password123", "password1234", "qwerty12345", "1234567890", "iloveyou123", "letmein1234",
        }),
    ]);

    public IReadOnlyList<PasswordFailure> Validate(string raw) =>
        rules
            .Select(rule => (rule.Code, Message: rule.Validate(raw)))
            .Where(r => r.Message is not null)
            .Select(r => new PasswordFailure(r.Code, r.Message!))
            .ToList();
}

public sealed class MinLengthRule(
    int minLength
) : IPasswordRule
{
    public string Code => "min-length";
    public string? Validate(string raw) =>
        raw.Length < minLength ? $"password must be at least {minLength} characters long" : null;
}

public sealed class HasUppercaseRule : IPasswordRule
{
    public string Code => "has-uppercase";
    public string? Validate(string raw) =>
        raw.Any(char.IsUpper) ? null : "password must contain at least one uppercase letter";
}

public sealed class HasDigitRule : IPasswordRule
{
    public string Code => "has-digit";
    public string? Validate(string raw) =>
        raw.Any(char.IsDigit) ? null : "password must contain at least one digit";
}

public sealed class HasSpecialCharRule : IPasswordRule
{
    public string Code => "has-special-char";
    public string? Validate(string raw) =>
        raw.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
            ? null
            : "password must contain at least one special character";
}

public sealed class NotCommonRule(
    IReadOnlySet<string> blacklist
) : IPasswordRule
{
    public string Code => "not-common";
    public string? Validate(string raw) => blacklist.Contains(raw) ? "password is too common" : null;
}
