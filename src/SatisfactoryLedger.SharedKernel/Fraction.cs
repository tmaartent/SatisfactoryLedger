using System.Numerics;

namespace SatisfactoryLedger.SharedKernel;

internal sealed record Fraction
{
    internal Fraction(BigInteger numerator, BigInteger denominator)
    {
        var num = numerator;
        var den = denominator;
        ArgumentOutOfRangeException.ThrowIfZero(den);
        
        if (den < 0)
        {
            num = -num;
            den = BigInteger.Abs(den);
        }
        
        var gcd = BigInteger.GreatestCommonDivisor(numerator, den);

        Numerator = num / gcd;
        Denominator = den / gcd;
    }
    
    public BigInteger Numerator { get; }

    public BigInteger Denominator { get; }

    public static Fraction FromDecimal(decimal d)
    {
        return Multiplication(d, 1);
    }

    private static Fraction Multiplication(decimal numerator, int denominator)
    {
        int decimalPlaces = numerator.Scale;

        var multiplication = (decimal)Math.Pow(10, decimalPlaces);

        return new Fraction(new BigInteger(numerator * multiplication), new BigInteger(denominator * multiplication));
    }

    public static Fraction FromPercent(decimal percent)
    {
        return Multiplication(percent, 100);
    }
}