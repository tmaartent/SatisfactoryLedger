using System.Numerics;

namespace SatisfactoryLedger.SharedKernel;

public sealed record ClockSpeed
{
    private readonly Fraction _fraction;

    private ClockSpeed(BigInteger numerator, BigInteger denominator)
    {
        this._fraction  = new Fraction(numerator, denominator);
    }
    
    public static ClockSpeed Percent(decimal percent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 250);

        var fraction = Fraction.FromPercent(percent);
        return new ClockSpeed(fraction.Numerator, fraction.Denominator);
    }

    public BigInteger Numerator => _fraction.Numerator;
    public BigInteger Denominator => _fraction.Denominator;
}