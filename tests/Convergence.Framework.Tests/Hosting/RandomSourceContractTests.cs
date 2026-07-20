using Convergence.Hosting;
using Convergence.Internal;
using Xunit;

namespace Convergence.Framework.Tests.Hosting;

public sealed class RandomSourceContractTests
{
    [Theory]
    [InlineData(-1, 0, 4)]
    [InlineData(4, 0, 4)]
    public void NextInt32_RejectsValuesOutsideTheRequestedHalfOpenRange(
        int returnedValue,
        int minimumInclusive,
        int maximumExclusive)
    {
        var random = new FixedRandomSource(returnedValue, 0m);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            RandomSourceContract.NextInt32(random, minimumInclusive, maximumExclusive));

        Assert.Contains($"[{minimumInclusive}, {maximumExclusive})", error.Message, StringComparison.Ordinal);
        Assert.Contains($"'{returnedValue}'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1")]
    public void NextUnitDecimal_RejectsValuesOutsideTheUnitInterval(string returnedValue)
    {
        decimal value = decimal.Parse(returnedValue, System.Globalization.CultureInfo.InvariantCulture);
        var random = new FixedRandomSource(0, value);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            RandomSourceContract.NextUnitDecimal(random));

        Assert.Contains("[0, 1)", error.Message, StringComparison.Ordinal);
        Assert.Contains($"'{value}'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Contract_ReturnsValidHostValuesUnchanged()
    {
        var random = new FixedRandomSource(3, 0.25m);

        Assert.Equal(3, RandomSourceContract.NextInt32(random, 2, 5));
        Assert.Equal(0.25m, RandomSourceContract.NextUnitDecimal(random));
    }

    private sealed class FixedRandomSource(int integer, decimal unit) : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => integer;

        public decimal NextUnitDecimal() => unit;
    }
}
