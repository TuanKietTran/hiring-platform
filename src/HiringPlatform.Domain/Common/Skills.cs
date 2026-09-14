namespace HiringPlatform.Domain.Common;

public static class Skills
{
    public const int MaxCount = 30;
    public const int MaxLength = 50;

    // trimmed, de-duplicated (case-insensitive), order preserved
    public static string[] Normalize(IEnumerable<string>? raw)
    {
        var skills = (raw ?? [])
            .Select(s => s?.Trim() ?? "")
            .Where(s => s.Length > 0)
            .DistinctBy(s => s.ToLowerInvariant())
            .ToArray();

        if (skills.Length > MaxCount)
            throw new DomainException($"at most {MaxCount} skills allowed");
        if (skills.Any(s => s.Length > MaxLength))
            throw new DomainException($"skills must be ≤ {MaxLength} characters");
        return skills;
    }
}
