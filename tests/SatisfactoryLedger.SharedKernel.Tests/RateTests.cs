namespace SatisfactoryLedger.SharedKernel.Tests;

public class RateTests
{
    [Fact]
    public void One_item_per_4_seconds_is_15_per_minute()
    {
        Assert.Equal(Rate.PerMinute(15), Rate.FromRecipe(1, 4));
    }

    [Fact]
    public void Rate_of_15_to_One_is_15_per_min()
    {
        Assert.Equal(Rate.PerMinute(15), Rate.Of(15, 1));
    }


    [Fact]
    public void Two_items_per_6_seconds_is_20_per_minute()
    {
        Assert.Equal(Rate.PerMinute(20), Rate.FromRecipe(2, 6));
    }


    [Fact]
    public void Three_items_per_32_seconds_is_non_round()
    {
        Assert.Equal(Rate.Of(45, 8), Rate.FromRecipe(3, 32));
    }
    
    [Fact]
    public void Per_minute_can_be_decimal()
    {
        Assert.Equal(Rate.Of(2421, 500), Rate.PerMinute(4.842m));
    }

    [Fact]
    public void Ninety_sixteenths_equals_forty_five_eighths()
    {
        Assert.Equal(Rate.Of(45, 8), Rate.Of(90, 16));
        Assert.Equal(Rate.Of(45, 8).GetHashCode(), Rate.Of(90, 16).GetHashCode());
    }

    [Fact]
    public void A_denominator_of_zero_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rate.Of(16, 0));
    }

    [Fact]
    public void Zero_seconds_will_throw_an_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rate.FromRecipe(15, 0));
    }

    [Fact]
    public void Zero_amount_will_throw_an_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rate.FromRecipe(0, 4));
    }

    [Fact]
    public void Negative_seconds_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rate.FromRecipe(15, -5));
    }

    [Fact]
    public void Negative_amount_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rate.FromRecipe(-15, 5));
    }

    [Fact]
    public void Negative_denominator_is_allowed_but_moved_to_numerator()
    {
        Assert.Equal(Rate.Of(-45, 8), Rate.Of(90, -16));
    }

    [Fact]
    public void ToString_prints_out_the_numerator_and_denominator()
    {
        Assert.Equal("Numerator: -45, Denominator: 8", Rate.Of(-45, 8).ToString());
    }

    [Fact]
    public void Rates_can_be_added()
    {
        var rate = Rate.PerMinute(15);

        Assert.Equal(Rate.PerMinute(30), rate + rate);
    }

    [Fact]
    public void Rates_can_be_subtracted()
    {
        var rate = Rate.PerMinute(15);

        Assert.Equal(Rate.PerMinute(10), rate - Rate.PerMinute(5));
    }

    [Fact]
    public void Rates_can_be_multiplied()
    {
        var rate = Rate.PerMinute(15);

        Assert.Equal(Rate.PerMinute(30), rate * 2);
    }

    [Fact]
    public void Rates_can_be_multiplied_by_fractions()
    {
        var rate = Rate.PerMinute(40);
        Assert.Equal(Rate.PerMinute(30), rate * ClockSpeed.Percent(75));
    }

    [Fact]
    public void Three_consumers_at_one_per_9s_exactly_drain_20_per_minute()
    {
        var rate = Rate.PerMinute(20);

        var sub = Rate.FromRecipe(1, 9);

        var emptyRate = rate - sub - sub - sub;

        Assert.Equal(Rate.Zero, emptyRate);
        
        Assert.True(emptyRate.IsZero());
    }

    [Fact]
    public void Rates_can_be_checked_to_be_Negative()
    {
        var rate = Rate.PerMinute(20);

        var sub = Rate.FromRecipe(1, 9);

        var emptyRate = rate - sub - sub - sub - sub;

        Assert.True(emptyRate.IsNegative());
    }

    [Fact]
    public void Greater_or_equal_is_false_when_less()
    {
        Assert.False(Rate.PerMinute(15) >= Rate.FromRecipe(2, 4));
    }
    
    [Fact]
    public void Greater_or_equal_is_true_when_more()
    {
        Assert.True(Rate.PerMinute(30) >= Rate.FromRecipe(1, 4));
    }
    
    [Fact]
    public void Greater_or_equal_is_true_when_equal()
    {
        Assert.True(Rate.PerMinute(15) >= Rate.FromRecipe(1, 4));
    }
    
    [Fact]
    public void Less_or_equal_is_false_when_more()
    {
        Assert.False(Rate.PerMinute(30) <= Rate.FromRecipe(1, 4));
    }
    
    [Fact]
    public void Less_or_equal_is_true_when_less()
    {
        Assert.True(Rate.PerMinute(15) <= Rate.FromRecipe(2, 4));
    }
    
    [Fact]
    public void Less_or_equal_is_true_when_equal()
    {
        Assert.True(Rate.PerMinute(15) <= Rate.FromRecipe(1, 4));
    }
    
    [Fact]
    public void Greater_is_false_when_less()
    {
        Assert.False(Rate.PerMinute(30) > Rate.FromRecipe(2, 4));
    }
    
    [Fact]
    public void Greater_is_true_when_more()
    {
        Assert.True(Rate.PerMinute(30) > Rate.FromRecipe(1, 4));
    }
    
    [Fact]
    public void Less_is_false_when_more()
    {
        Assert.False(Rate.PerMinute(30) < Rate.FromRecipe(1, 4));
    }
    
    [Fact]
    public void Less_is_true_when_less()
    {
        Assert.True(Rate.PerMinute(15) < Rate.FromRecipe(2, 4));
    }
    
    [Fact]
    public void Rates_can_be_overclocked()
    {
        Assert.Equal(Rate.PerMinute(50), Rate.PerMinute(20) * ClockSpeed.Percent(250));
    }
    
    [Fact]
    public void Rates_can_be_underclocked()
    {
        Assert.Equal(Rate.PerMinute(10), Rate.PerMinute(20) * ClockSpeed.Percent(50));
    }

    [Fact] public void Clockspeed_100_has_no_effect()
    {
        Assert.Equal(Rate.PerMinute(20), Rate.PerMinute(20) * ClockSpeed.Percent(100));
    }
}
