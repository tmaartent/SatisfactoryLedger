namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record RoutedBelt(string From, string To, WorldPoint[] Corners, int[] Levels, string? Item, double? PerMinute);
