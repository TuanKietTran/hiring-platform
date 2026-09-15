namespace HiringPlatform.Domain.Applicants;

using HiringPlatform.Domain.Common;

/// <summary>The applicant-owned professional profile and its versioned CV links.</summary>
public sealed record CandidateProfile
{
    private CandidateProfile()
    {
    }

    public UserId CandidateId
    {
        get; private init;
    }
    public string Headline { get; private init; } = "";
    public string Summary { get; private init; } = "";
    public string Location { get; private init; } = "";
    public PhoneNumber? Phone
    {
        get; private init;
    }
    public IReadOnlyList<string> Skills { get; private init; } = [];
    public IReadOnlyList<CvDocument> Cvs { get; private init; } = [];
    public DateTimeOffset UpdatedAt
    {
        get; private init;
    }

    public static CandidateProfile Create(UserId candidateId, DateTimeOffset now) => new()
    {
        CandidateId = candidateId,
        UpdatedAt = now.ToUniversalTime(),
    };

    public CandidateProfile Update(
        string? headline, string? summary, string? location, string? phone, string? phoneCallingCode,
        IEnumerable<string>? skills, DateTimeOffset now) => this with
        {
            Headline = RequiredLength(headline, "Headline", 160, allowEmpty: true),
            Summary = RequiredLength(summary, "Summary", 2_000, allowEmpty: true),
            Location = RequiredLength(location, "Location", 160, allowEmpty: true),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : PhoneNumber.Create(phone, phoneCallingCode),
            Skills = global::HiringPlatform.Domain.Common.Skills.Normalize(skills),
            UpdatedAt = now.ToUniversalTime(),
        };

    public CandidateProfile AddCv(Guid id, string name, string url, DateTimeOffset now)
    {
        var safeName = RequiredLength(name, "CV name", 120, allowEmpty: false);
        if (!TryCvLocation(url, out var uri))
            throw new DomainException("CV location must be an absolute HTTPS URL or an owned blob URL");
        if (Cvs.Any(x => x.Id == id))
            throw new DomainException("CV already exists");

        var makePrimary = Cvs.Count == 0;
        return this with
        {
            Cvs = [.. Cvs.Select(x => makePrimary ? x with { IsPrimary = false } : x), new CvDocument(id, safeName, uri, makePrimary, now.ToUniversalTime())],
            UpdatedAt = now.ToUniversalTime(),
        };
    }

    public CandidateProfile SetPrimaryCv(Guid id, DateTimeOffset now)
    {
        if (!Cvs.Any(x => x.Id == id))
            throw new DomainException("CV not found");
        return this with
        {
            Cvs = [.. Cvs.Select(x => x with { IsPrimary = x.Id == id })],
            UpdatedAt = now.ToUniversalTime()
        };
    }

    public CandidateProfile RemoveCv(Guid id, DateTimeOffset now)
    {
        var removed = Cvs.SingleOrDefault(x => x.Id == id) ?? throw new DomainException("CV not found");
        var remaining = Cvs.Where(x => x.Id != id).ToList();
        if (removed.IsPrimary && remaining.Count > 0)
            remaining[0] = remaining[0] with
            {
                IsPrimary = true
            };
        return this with
        {
            Cvs = remaining,
            UpdatedAt = now.ToUniversalTime()
        };
    }

    public CandidateProfileSnapshot ToSnapshot() => new(
        CandidateId.Value, Headline, Summary, Location, Phone?.Digits, Phone?.CallingCode, Skills,
        [.. Cvs.Select(x => new CvDocumentSnapshot(x.Id, x.Name, x.Url.ToString(), x.IsPrimary, x.AddedAt))], UpdatedAt);

    public static CandidateProfile FromSnapshot(CandidateProfileSnapshot s) => new()
    {
        CandidateId = new UserId(s.CandidateId),
        Headline = s.Headline,
        Summary = s.Summary,
        Location = s.Location,
        Phone = string.IsNullOrWhiteSpace(s.PhoneDigits) ? null : PhoneNumber.Create(s.PhoneDigits, s.PhoneCallingCode),
        Skills = s.Skills,
        Cvs = [.. s.Cvs.Select(x => new CvDocument(x.Id, x.Name, new Uri(x.Url, UriKind.RelativeOrAbsolute), x.IsPrimary, x.AddedAt))],
        UpdatedAt = s.UpdatedAt,
    };

    private static bool TryCvLocation(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out uri!) && uri.Scheme == Uri.UriSchemeHttps)
            return true;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Relative, out uri!))
            return false;
        var segments = uri.OriginalString.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments is ["api", "blobs", var id] && Guid.TryParse(id, out _);
    }

    private static string RequiredLength(string? value, string field, int max, bool allowEmpty)
    {
        var text = value?.Trim() ?? "";
        if (!allowEmpty && text.Length == 0)
            throw new DomainException($"{field} must not be empty");
        if (text.Length > max)
            throw new DomainException($"{field} must be ≤ {max} characters");
        return text;
    }
}

public sealed record CvDocument(
    Guid Id,
    string Name,
    Uri Url,
    bool IsPrimary,
    DateTimeOffset AddedAt
);
public sealed record CvDocumentSnapshot(
    Guid Id,
    string Name,
    string Url,
    bool IsPrimary,
    DateTimeOffset AddedAt
);
public sealed record CandidateProfileSnapshot(
    Guid CandidateId,
    string Headline,
    string Summary,
    string Location,
    string? PhoneDigits,
    string? PhoneCallingCode,
    IReadOnlyList<string> Skills,
    IReadOnlyList<CvDocumentSnapshot> Cvs,
    DateTimeOffset UpdatedAt
);
