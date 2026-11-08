namespace SatisfactoryLedger.World.Domain.Tests;

internal static class Ids
{
    public static IEnumerable<BuildableId> Of(params string[] ids) => ids.Select(BuildableId.Of);

    /// <summary>Asserts the set of ids regardless of order. A plain Assert.Equal on two sets compares them in order.</summary>
    public static void AssertSet(IEnumerable<BuildableId> actual, params string[] expected) =>
        Assert.Equal(expected.Order(), actual.Select(id => id.Id).Order());
}