using HelloDotnet;
using Xunit;

namespace HelloDotnet.Tests;

public class GreetingTests
{
    [Fact]
    public void Greet_ReturnsExpectedMessage()
    {
        Assert.Equal("Hello, .NET!", Greeting.Greet(".NET"));
        Assert.Equal("Hello, CI!", Greeting.Greet("CI"));
    }

    [Fact]
    public void SumRange_ReturnsCorrectSum()
    {
        Assert.Equal(55, Greeting.SumRange(1, 10));
        Assert.Equal(5050, Greeting.SumRange(1, 100));
    }
}
