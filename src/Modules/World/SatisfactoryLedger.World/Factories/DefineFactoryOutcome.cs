namespace SatisfactoryLedger.World.Factories;

/// <summary>What defining a factory can come to. A closed set: the endpoint handles each case.</summary>
internal abstract record DefineFactoryOutcome
{
    private DefineFactoryOutcome()
    {
    }

    internal sealed record Defined(StoredFactory Factory) : DefineFactoryOutcome;

    /// <summary>No snapshot has this session name, so there's no world to define it in.</summary>
    internal sealed record UnknownWorld : DefineFactoryOutcome;

    /// <summary>Ids that aren't machines or generators in the world's latest snapshot (BR-W2).</summary>
    internal sealed record NotMachines(IReadOnlyList<string> Ids) : DefineFactoryOutcome;

    /// <summary>The output belt isn't a belt fed by machines in the world's latest snapshot.</summary>
    internal sealed record NotAnOutputBelt : DefineFactoryOutcome;

    /// <summary>Input items the Catalog doesn't know: a typo, most likely (BR-W2).</summary>
    internal sealed record UnknownInputItems(IReadOnlyList<string> Items) : DefineFactoryOutcome;

    /// <summary>Chosen machines another factory of the world already has (BR-W5).</summary>
    internal sealed record MachinesTaken(IReadOnlyList<TakenMachine> Machines) : DefineFactoryOutcome;

    /// <summary>The world already has a factory with this name (BR-W11).</summary>
    internal sealed record NameTaken : DefineFactoryOutcome;
}