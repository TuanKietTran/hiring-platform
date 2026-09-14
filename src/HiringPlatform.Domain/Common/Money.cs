namespace HiringPlatform.Domain.Common;

/// <summary>ISO 4217 currency codes supported by this system.</summary>
public enum Currency
{
    USD, EUR, GBP, JPY, AUD, CAD, SGD, VND
}

/// <summary>Amount in minor units plus currency. Ported from cv-sv subscription/money.ts.</summary>
public sealed record Money
{
    // minor-unit multipliers (JPY + VND have no subunit)
    private static readonly Dictionary<Currency, int> MinorUnits = new()
    {
        [Currency.USD] = 100,
        [Currency.EUR] = 100,
        [Currency.GBP] = 100,
        [Currency.AUD] = 100,
        [Currency.CAD] = 100,
        [Currency.SGD] = 100,
        [Currency.JPY] = 1,
        [Currency.VND] = 1,
    };

    private Money(long amountMinor, Currency currency)
    {
        AmountMinor = amountMinor;
        Currency = currency;
    }

    public long AmountMinor
    {
        get;
    }
    public Currency Currency
    {
        get;
    }

    public decimal AmountMajor => (decimal)AmountMinor / MinorUnits[Currency];

    public static Money Of(long amountMinor, Currency currency)
    {
        if (amountMinor < 0)
            throw new DomainException("amountMinor must be non-negative");
        if (!Enum.IsDefined(currency))
            throw new DomainException($"unsupported currency: {currency}");
        return new Money(amountMinor, currency);
    }

    // convenience: pass human amount, e.g. Money.FromMajor(9.99m, Currency.USD) → 999
    public static Money FromMajor(decimal amount, Currency currency)
    {
        if (!Enum.IsDefined(currency))
            throw new DomainException($"unsupported currency: {currency}");
        return Of((long)Math.Round(amount * MinorUnits[currency], MidpointRounding.AwayFromZero), currency);
    }

    public Money Add(Money other)
    {
        AssertSameCurrency(other);
        return Of(AmountMinor + other.AmountMinor, Currency);
    }

    public Money Multiply(decimal factor)
    {
        if (factor < 0)
            throw new DomainException("factor must be non-negative");
        return Of((long)Math.Round(AmountMinor * factor, MidpointRounding.AwayFromZero), Currency);
    }

    public bool IsGreaterThan(Money other)
    {
        AssertSameCurrency(other);
        return AmountMinor > other.AmountMinor;
    }

    private void AssertSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException($"currency mismatch: {Currency} vs {other.Currency}");
    }

    public override string ToString() =>
        MinorUnits[Currency] == 1 ? $"{AmountMajor:0} {Currency}" : $"{AmountMajor:0.00} {Currency}";
}
