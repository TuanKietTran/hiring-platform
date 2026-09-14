using System.Text.RegularExpressions;

namespace HiringPlatform.Domain.Common;

/// <summary>
/// Local digits plus a numeric calling code. Simplified port of cv-sv phone/phone.ts
/// (generic 4–15 digit validation, no per-country formats).
/// </summary>
public sealed partial record PhoneNumber
{
    private PhoneNumber(string digits, string callingCode)
    {
        Digits = digits;
        CallingCode = callingCode;
    }

    public string Digits
    {
        get;
    }
    public string CallingCode
    {
        get;
    }

    public string International => $"+{CallingCode}{Digits}";

    public static PhoneNumber Create(string? raw, string? callingCode)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new DomainException("phone number is required");
        if (!AllowedChars().IsMatch(raw))
            throw new DomainException("phone number contains invalid characters");

        var code = callingCode?.Trim().TrimStart('+') ?? "";
        if (!CallingCodePattern().IsMatch(code))
            throw new DomainException("calling code must be 1–3 digits");

        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        // callers sometimes pass an already-international number; strip the calling code once
        if (raw.TrimStart().StartsWith('+') && digits.StartsWith(code, StringComparison.Ordinal))
            digits = digits[code.Length..];

        if (digits.Length is < 4 or > 15)
            throw new DomainException("phone number must have 4–15 digits");
        return new PhoneNumber(digits, code);
    }

    public override string ToString() => International;

    [GeneratedRegex(@"^[\d\s()+\-.]+$")]
    private static partial Regex AllowedChars();

    [GeneratedRegex(@"^\d{1,3}$")]
    private static partial Regex CallingCodePattern();
}
