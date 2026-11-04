using System.Globalization;
using System.Numerics;

namespace SatisfactoryLedger.SharedKernel;

public record Rate: IComparable<Rate> {
    private readonly Fraction _fraction;

    private Rate(BigInteger numerator, BigInteger denominator)
    {
        this._fraction  = new Fraction(numerator, denominator);
    }

    public static Rate FromRecipe(decimal amount, decimal cycleSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cycleSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        var amountPerMinute = Fraction.FromDecimal(amount * 60);
        var cycleSecondsFraction = Fraction.FromDecimal(cycleSeconds);

        // (a/b) / (c/d) = (a·d) / (b·c)
        return new Rate(
            amountPerMinute.Numerator * cycleSecondsFraction.Denominator,
            amountPerMinute.Denominator * cycleSecondsFraction.Numerator);
    }

    public static Rate Of(BigInteger numerator, BigInteger denominator)
    {
        return new Rate(numerator, denominator);
    }

    
    public static Rate PerMinute(decimal amount)
    {
        var fraction = Fraction.FromDecimal(amount);
        
        return Of(fraction.Numerator, fraction.Denominator);
    }
    
    public static Rate operator +(Rate a, Rate b)
    {
        var newNumerator = (a.Numerator * b.Denominator) + (b.Numerator * a.Denominator);
        var newDenominator = a.Denominator * b.Denominator;
        
        return Of(newNumerator, newDenominator);
    }

    public static Rate operator -(Rate a, Rate b)
    {
        var newNumerator = (a.Numerator * b.Denominator) - (b.Numerator * a.Denominator);
        var newDenominator = a.Denominator * b.Denominator;
        
        return Of(newNumerator, newDenominator);
    }

    public static Rate Zero => PerMinute(0);
    
    public static Rate operator *(Rate a, int times)
    {
        var newNumerator = a.Numerator * times;
        var newDenominator = a.Denominator;
        
        return Of(newNumerator, newDenominator);
    }
    
    public static Rate operator *(Rate a, ClockSpeed clockSpeed)
    {
        var newNumerator = a.Numerator * clockSpeed.Numerator;
        var newDenominator = a.Denominator * clockSpeed.Denominator;
        
        return Of(newNumerator, newDenominator);
    }

    public static bool operator <(Rate a, Rate b)
    {
        return a.CompareTo(b) < 0;
    }
    
    public static bool operator <=(Rate a, Rate b)
    {
        return a.CompareTo(b) <= 0;
    }
    
    public static bool operator >(Rate a, Rate b)
    {
        return a.CompareTo(b) > 0;
    }
    
    public static bool operator >=(Rate a, Rate b)
    {
        return a.CompareTo(b) >= 0;
    }
    
    public bool IsZero()
    {
        return this.Numerator == 0;
    }

    public bool IsNegative()
    {
        return this.Numerator < 0;
    }

    private BigInteger Numerator => _fraction.Numerator;
    private BigInteger Denominator => _fraction.Denominator;
    
    public int CompareTo(Rate? other)
    {
        return other != null ? (this.Numerator * other.Denominator).CompareTo(other.Numerator * this.Denominator) : 1;
    }

    public decimal ToDecimal()
    {
        return (decimal)Numerator / (decimal)Denominator;
    }

    public override string ToString()
    {
        var x = Math.Round(this.ToDecimal(), 3, MidpointRounding.AwayFromZero);
        return x.ToString(CultureInfo.InvariantCulture);
    }
}