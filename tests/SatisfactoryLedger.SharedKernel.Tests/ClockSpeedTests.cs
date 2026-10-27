namespace SatisfactoryLedger.SharedKernel.Tests;

public class ClockSpeedTests
{
    [Theory]
    [InlineData(1, 2, 50)]
    [InlineData(231, 200, 115.5)]
    [InlineData(333333, 1000000, 33.3333)]
    [InlineData(1, 100, 1)]
    [InlineData(5, 2, 250)]

    public void ClockSpeed_percent_can_be_expressed_in_rates(int numerator, int denominator, decimal percent)
    {
        Assert.Equal(numerator, ClockSpeed.Percent(percent).Numerator);
        Assert.Equal(denominator, ClockSpeed.Percent(percent).Denominator);
    }
    
    [Fact]
    public void ClockSpeed_percent_cannot_be_less_than_1()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClockSpeed.Percent((decimal)0.5));
    }
    
    
    [Fact]
    public void ClockSpeed_percent_cannot_be_more_than_250()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClockSpeed.Percent(251));
    }
}