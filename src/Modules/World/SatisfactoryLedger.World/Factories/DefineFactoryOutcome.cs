namespace SatisfactoryLedger.World.Factories;

/// <summary>What defining or editing a factory can come to. A closed set: the endpoints handle each case.</summary>
internal abstract record DefineFactoryOutcome
{
    private DefineFactoryOutcome()
    {
    }

    internal sealed record Defined(StoredFactory Factory) : DefineFactoryOutcome;

    /// <summary>The factory to edit isn't stored.</summary>
    internal sealed record UnknownFactory : DefineFactoryOutcome;

    /// <summary>No snapshot has this session name, so there's no world to define it in.</summary>
    internal sealed record UnknownWorld : DefineFactoryOutcome;

    /// <summary>Ids that aren't machines or generators in the world's latest snapshot (BR-W2).</summary>
    internal sealed record NotMachines(IReadOnlyList<string> Ids) : DefineFactoryOutcome;

    /// <summary>Chosen machines another factory of the world already has (BR-W5).</summary>
    internal sealed record MachinesTaken(IReadOnlyList<TakenMachine> Machines) : DefineFactoryOutcome;

    /// <summary>The world already has a factory with this name (BR-W11).</summary>
    internal sealed record NameTaken : DefineFactoryOutcome;
}