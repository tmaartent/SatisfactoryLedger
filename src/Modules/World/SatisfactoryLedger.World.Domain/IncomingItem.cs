using System.Diagnostics.CodeAnalysis;

namespace SatisfactoryLedger.World.Domain;

/// <summary>What a factory takes in of one item (decided 2026-11-11): one item of its <see cref="FactoryHandovers.Inputs"/>.</summary>
public sealed class IncomingItem
{
    /// <summary>
    /// A shortfall up to this share of the need is none: a <em>relative</em> tolerance, 0.01% of the need. The flow
    /// settles within a small tolerance, while the need comes exactly from the recipes. Not the absolute
    /// <see cref="Flow.SteadyState.Tolerance"/> per minute the solver settles to.
    /// </summary>
    private const double ShortfallShareOfNeed = 1e-4;

    private readonly Func<double> _couldBringMore;

    /// <param name="couldBringMore">What its sources could bring more per minute, worked out when it's asked for.</param>
    internal IncomingItem(ItemId item, double arriving, double need, IReadOnlyList<IncomingSource> sources, Func<double> couldBringMore)
    {
        Item = item;
        Arriving = arriving;
        Need = need;
        Sources = sources;
        _couldBringMore = couldBringMore;
    }

    public ItemId Item { get; }

    /// <summary>What arrives over its inputs now, per minute.</summary>
    public double Arriving { get; }

    /// <summary>
    /// What it needs from outside per minute at 100%: what all its machines take of it, less what its own machines make
    /// of it.
    /// </summary>
    public double Need { get; }

    /// <summary>Where what arrives comes from, most first: unrounded, so a source of next to nothing is still here.</summary>
    public IReadOnlyList<IncomingSource> Sources { get; }

    /// <summary>
    /// What it would need more at 100% than arrives now and its sources could bring more, as far as the lines between
    /// have room (decided 2026-11-11). Worked out each time it's read: a maximum flow over the lines upstream, which
    /// not every reader needs.
    /// </summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Short is the glossary term, not the integer type.")]
    public double Short()
    {
        double shortfall = Need - (Arriving + _couldBringMore());
        return shortfall > Need * ShortfallShareOfNeed ? shortfall : 0;
    }
}
