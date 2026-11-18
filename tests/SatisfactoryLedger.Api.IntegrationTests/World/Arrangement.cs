namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record Arrangement(ArrangementSummary Summary, ArrangedFactory[] Factories, ArrangementLink[] Links);
