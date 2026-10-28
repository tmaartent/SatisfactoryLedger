namespace SatisfactoryLedger.SharedKernel.Tests;

public class ArrayArgumentExceptionTests
{
    private sealed class Dummy
    {
        public Dummy(string[]? input)
        {
            ArrayArgumentException.ThrowIfLessThan(input, 2);
            ArrayArgumentException.ThrowIfMoreThan(input, 4);

        }
    }
    [Fact]
    public void ArrayArgumentException_throws_when_argument_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => new Dummy(null));
    }
    
    [Fact]
    public void ArrayArgumentException_throws_when_argument_is_less_than_required()
    {
        Assert.Throws<ArrayArgumentException>(() => new Dummy(["Foo"]));
    }
    
    [Fact]
    public void ArrayArgumentException_throws_when_argument_is_more_than_required()
    {
        Assert.Throws<ArrayArgumentException>(() => new Dummy(["Foo", "Foo", "Foo", "Foo", "Foo", ]));
    }
}