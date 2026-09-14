namespace HiringPlatform.Domain.Common;

// Strongly typed identifiers. Version 7 GUIDs sort by creation time, which keeps indexes tidy.

public readonly record struct UserId(
    Guid Value
)
{
    public static UserId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct CompanyId(
    Guid Value
)
{
    public static CompanyId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct JobId(
    Guid Value
)
{
    public static JobId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct ApplicationId(
    Guid Value
)
{
    public static ApplicationId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct InterviewId(
    Guid Value
)
{
    public static InterviewId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
