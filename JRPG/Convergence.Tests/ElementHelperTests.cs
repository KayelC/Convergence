using JRPGPrototype.Core;
using Xunit;

namespace Convergence.Tests;

public sealed class ElementHelperTests
{
    [Theory]
    [InlineData("Slash Physical", Element.Slash)]
    [InlineData("Strike", Element.Strike)]
    [InlineData("Pierce", Element.Pierce)]
    [InlineData("Fire Magic", Element.Fire)]
    [InlineData("Ice", Element.Ice)]
    [InlineData("Elec", Element.Elec)]
    [InlineData("Wind", Element.Wind)]
    [InlineData("Earth", Element.Earth)]
    [InlineData("Light", Element.Light)]
    [InlineData("Dark", Element.Dark)]
    [InlineData("Mind", Element.Mind)]
    [InlineData("Nerve", Element.Nerve)]
    [InlineData("Curse", Element.Curse)]
    [InlineData("", Element.Almighty)]
    [InlineData("Unknown", Element.Almighty)]
    public void FromCategory_MapsKnownCategories(string category, Element expected)
    {
        Assert.Equal(expected, ElementHelper.FromCategory(category));
    }

    [Theory]
    [InlineData("Electric", Element.Elec)]
    [InlineData("Darkness", Element.Dark)]
    [InlineData("Fire", Element.Fire)]
    [InlineData("not-real", Element.None)]
    public void ParseElement_MapsAliasesAndUnknowns(string input, Element expected)
    {
        Assert.Equal(expected, ElementHelper.ParseElement(input));
    }

    [Theory]
    [InlineData("Reflect", Affinity.Repel)]
    [InlineData("Block", Affinity.Null)]
    [InlineData("Absorb", Affinity.Absorb)]
    [InlineData("Weak", Affinity.Weak)]
    [InlineData("not-real", Affinity.Normal)]
    public void ParseAffinity_MapsAliasesAndUnknowns(string input, Affinity expected)
    {
        Assert.Equal(expected, ElementHelper.ParseAffinity(input));
    }
}
