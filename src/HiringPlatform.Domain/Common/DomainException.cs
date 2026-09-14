namespace HiringPlatform.Domain.Common;

/// <summary>Raised when a domain invariant is violated. Handlers surface it as a validation failure.</summary>
public class DomainException(
    string message
) : Exception(message);

public sealed class InvalidTransitionException(
    string from,
    string to
)
    : DomainException($"invalid transition: {from} → {to}");
