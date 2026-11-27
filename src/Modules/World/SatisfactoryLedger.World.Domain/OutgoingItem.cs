using System.Diagnostics.CodeAnalysis;

namespace SatisfactoryLedger.World.Domain;

/// <summary>What a factory makes of one item (BR-A14, BR-A18): one item of its <see cref="FactoryHandovers.Outputs"/>.</summary>
public sealed class OutgoingItem
{
    internal OutgoingItem(
        ItemId item,
        double now,
        double atFullSpeed,
        double receiversNeed,
        double othersBring,
        double @short,
        double sunk,
        double cannotLeave,
        IReadOnlyList<OutgoingDestination> destinations)
    {
        Item = item;
        Now = now;
        AtFullSpeed = atFullSpeed;
        ReceiversNeed = receiversNeed;
        OthersBring = othersBring;
        Short = @short;
        Sunk = sunk;
        CannotLeave = cannotLeave;
        Destinations = destinations;
    }

    public ItemId Item { get; }

    /// <summary>What leaves over its outputs now, per minute.</summary>
    public double Now { get; }

    /// <summary>
    /// What would leave per minute at 100%: what its machines make of it at their clock speeds, less what its own
    /// machines take of it.
    /// </summary>
    public double AtFullSpeed { get; }

    /// <summary>
    /// What the machines and generators its outputs reach would take of it per minute at 100%, from whatever source:
    /// each once, however many of its lines reach it (decided 2026-11-11).
    /// </summary>
    public double ReceiversNeed { get; }

    /// <summary>What those receivers take of it now from other sources: what they take now, less what it brings them.</summary>
    public double OthersBring { get; }

    /// <summary>What those receivers would take more at 100% than they take now, from all sources together.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Short is the glossary term, not the integer type.")]
    public double Short { get; }

    /// <summary>What of it reaches an AWESOME Sink per minute.</summary>
    public double Sunk { get; }

    /// <summary>What its output blocked machines would make more of it per minute at 100%, but can't get out.</summary>
    public double CannotLeave { get; }

    /// <summary>Where what leaves goes, most first: unrounded, so a destination of next to nothing is still here.</summary>
    public IReadOnlyList<OutgoingDestination> Destinations { get; }
}
