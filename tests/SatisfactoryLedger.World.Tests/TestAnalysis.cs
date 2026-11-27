using SatisfactoryLedger.World.Contracts.Analysis;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// A stored analysis (ADR-0017) built by hand, for findings tested without the solver (issue #47): how each machine
/// runs, what flows into each belt, and what providers deliver. Only what a test needs is set; the rest is empty, as
/// the solver leaves what it has nothing to say about.
/// </summary>
internal sealed class TestAnalysis
{
    private readonly List<MachineStateDocument> _machines = [];
    private readonly List<UnusedSupplyDocument> _unused = [];
    private readonly Dictionary<string, Dictionary<string, double>> _into = new(StringComparer.Ordinal);

    /// <summary>A machine or generator that runs at full speed.</summary>
    public TestAnalysis Running(string id) => Machine(new MachineStateDocument(id, 1, MachineLimit.None));

    /// <summary>A machine or generator offered only <paramref name="share"/> of an input it needs.</summary>
    public TestAnalysis Starved(string id, string item, double share) => Machine(new MachineStateDocument(
        id, share, MachineLimit.Starved, item, new Dictionary<string, double> { [item] = share }, new Dictionary<string, double>()));

    /// <summary>A machine of which only <paramref name="share"/> of a product can leave.</summary>
    public TestAnalysis Blocked(string id, string item, double share) => Machine(new MachineStateDocument(
        id, share, MachineLimit.OutputBlocked, item, new Dictionary<string, double>(), new Dictionary<string, double> { [item] = share }));

    /// <summary>What flows into a belt, lift, pipe or container of an item, per minute, beside what else flows into it.</summary>
    public TestAnalysis Into(string id, string item, double perMinute)
    {
        (_into.TryGetValue(id, out Dictionary<string, double>? flows) ? flows : _into[id] = new(StringComparer.Ordinal))[item] = perMinute;
        return this;
    }

    /// <summary>A provider that delivers less of an item than it offers.</summary>
    public TestAnalysis Unused(string id, string item, double offered, double delivered)
    {
        _unused.Add(new UnusedSupplyDocument(id, item, offered, delivered));
        return this;
    }

    /// <summary>The analysis, as the database stores it and reads it back.</summary>
    public AnalysisDocument Build() => AnalysisDocument.FromJson(new AnalysisDocument(
        AnalysisDocument.CurrentVersion,
        _machines,
        [],
        _unused,
        [],
        [],
        _into.ToDictionary(entry => entry.Key, entry => (IReadOnlyDictionary<string, double>)entry.Value, StringComparer.Ordinal)).ToJson());

    private TestAnalysis Machine(MachineStateDocument state)
    {
        _machines.Add(state);
        return this;
    }
}
