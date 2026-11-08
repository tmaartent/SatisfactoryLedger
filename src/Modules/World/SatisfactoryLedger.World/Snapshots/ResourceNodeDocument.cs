namespace SatisfactoryLedger.World.Snapshots;

/// <param name="Purity"><c>impure</c>, <c>normal</c> or <c>pure</c>, as the Catalog writes it.</param>
internal sealed record ResourceNodeDocument(string Name, string Resource, string Purity);