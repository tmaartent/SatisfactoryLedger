using Google.OrTools.LinearSolver;

using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// The sequence of rounds (ADR-0026) on one linear programme: each round a guess of which shares are capped, changed
/// in place by <see cref="Cap"/>.
/// <list type="bullet">
/// <item>Variables: the flow of each item over each connection it can reach (<see cref="ItemsOnConnections"/>), a
/// utilisation between 0 and 1 per machine, and a level per <see cref="SharingGroup"/>.</item>
/// <item>A machine takes <c>demand × u</c> of each input (at most that of a stocked one, BR-A17) and makes
/// <c>production × u</c> of each product (BR-A1).</item>
/// <item>Logistics passes on what comes in, per item; a belt, lift or pipe at most its capacity (BR-A3). A provider
/// delivers at most its offer (BR-A2); a source of unknown rate at most what it's asked (BR-A9). Sinks take everything.</item>
/// <item>Sharing (BR-A4, A5): a share that isn't capped carries its group's level; a capped one at most that.</item>
/// </list>
/// Three objectives, each solved with the ones before it held: <see cref="Push"/>, <see cref="CappedShares"/>,
/// <see cref="Utilisation"/>. Every objective also has a tiny penalty on every flow, so items don't circle without
/// reason.
/// </summary>
internal sealed class LinearProgramme : IDisposable
{
    /// <summary>Against flows that circle in a loop of logistics for nothing: small enough never to outweigh real flow.</summary>
    private const double Circling = 1e-6;

    /// <summary>A share is capped when holding it to its level costs more than this per item: well above the penalty.</summary>
    private const double CostsDelivery = 1e-4;

    private readonly FlowNetwork _network;
    private readonly ItemsOnConnections _items;
    private readonly Solver _solver;
    private readonly Dictionary<(int Edge, ItemId Item), Variable> _flow = [];
    private readonly Dictionary<int, Variable> _utilisation = [];
    private readonly Dictionary<(int Group, int Edge), (Variable Below, List<Variable> Carried, int Tier)> _uncappedShares = [];
    private readonly Dictionary<int, (Variable Blocked, List<(int Group, int Edge)> Waiting)> _blockedTiers = [];
    private readonly List<(Variable Variable, double Coefficient)> _push = [];
    private readonly SortedDictionary<int, List<(Variable Variable, double Coefficient)>> _cappedShares = [];
    private readonly List<int>[] _into;
    private readonly List<int>[] _outOf;
    private readonly Dictionary<int, Dictionary<ItemId, Constraint>> _takes = [];
    private readonly Dictionary<int, Dictionary<ItemId, Constraint>> _makes = [];
    private readonly Dictionary<int, Constraint> _capacity = [];
    private readonly Dictionary<int, List<Constraint>> _conserved = [];
    private readonly Dictionary<int, List<Variable>> _belowOf = [];
    private readonly HashSet<(int Group, int Edge)> _capped;
    private readonly IReadOnlyList<SharingGroup> _groups;
    private readonly Dictionary<int, Variable> _levels = [];
    private List<Action>? _restore;

    /// <summary>
    /// The round's solution, read before any what-if. OR-Tools counts a solution as out of date once the model changes:
    /// after the first bound is relaxed, <c>SolutionValue()</c> reads 0. What-ifs freeze to these values instead.
    /// </summary>
    private Dictionary<Variable, double> _solved = [];

    public LinearProgramme(
        FlowNetwork network, ItemsOnConnections items, IReadOnlyList<SharingGroup> groups, IReadOnlySet<(int Group, int Edge)> capped)
    {
        _network = network;
        _items = items;
        _capped = [.. capped];
        _groups = groups;
        _solver = Solver.CreateSolver("GLOP") ?? throw new InvalidOperationException("OR-Tools has no GLOP solver");
        _into = Lists(edge => edge.To);
        _outOf = Lists(edge => edge.From);

        for (int edge = 0; edge < network.Edges.Count; edge++)
        {
            foreach (ItemId item in items.Items[edge])
            {
                _flow[(edge, item)] = _solver.MakeNumVar(0, double.PositiveInfinity, $"f{edge}:{item}");
            }
        }

        for (int node = 0; node < network.Nodes.Count; node++)
        {
            Build(node, _into[node], _outOf[node]);
        }

        for (int group = 0; group < groups.Count; group++)
        {
            Share(group, groups[group], capped);
            Wait(group, groups, capped);
        }
    }

    /// <summary>What sources push in: provider deliveries, and what sources of unknown rate are asked for.</summary>
    public IReadOnlyList<(Variable Variable, double Coefficient)> Push => _push;

    /// <summary>
    /// What the capped shares carry, per tier, the first tier first: each takes all its far end allows, as the game's
    /// min(take, share), and a tier served first before the ones after it.
    /// </summary>
    public IEnumerable<IReadOnlyList<(Variable Variable, double Coefficient)>> CappedShares => _cappedShares.Values;

    /// <summary>Every machine's utilisation: what runs a loop no source pushes, such as one fed only by itself.</summary>
    public IReadOnlyList<(Variable Variable, double Coefficient)> Utilisation => [.. _utilisation.Values.Select(u => (u, 1.0))];

    /// <summary>
    /// Caps these shares for the next round: each may now fall below its level. A tier whose shares above are all
    /// capped is no longer blocked. Only bounds change, so the solver starts the next round from this one's solution.
    /// </summary>
    public void Cap(IEnumerable<(int Group, int Edge)> shares)
    {
        foreach ((int Group, int Edge) share in shares)
        {
            _capped.Add(share);
            if (_uncappedShares.Remove(share, out (Variable Below, List<Variable> Carried, int Tier) uncapped))
            {
                uncapped.Below.SetBounds(0, double.PositiveInfinity);
                AddCapped(uncapped.Tier, uncapped.Carried);
            }

            foreach ((int group, (Variable blocked, List<(int Group, int Edge)> waiting)) in _blockedTiers.ToList())
            {
                waiting.Remove(share);
                if (waiting.Count == 0)
                {
                    blocked.SetBounds(0, double.PositiveInfinity);
                    _blockedTiers.Remove(group);
                }
            }
        }
    }

    /// <summary>The shares capped so far: a copy of this programme built with them settles the same way.</summary>
    public IReadOnlySet<(int Group, int Edge)> Capped => _capped;

    /// <summary>
    /// Once no share holds the sources back: the three objectives in order, each held for the next. What the sources
    /// push in, the capped shares tier by tier (the first tier first), then every machine's utilisation.
    /// </summary>
    public void Settle()
    {
        Keep(Push, Maximise(Push));
        foreach (IReadOnlyList<(Variable Variable, double Coefficient)> tier in CappedShares.ToList())
        {
            Keep(tier, Maximise(tier));
        }

        Maximise(Utilisation);
    }

    /// <summary>Solves for the most of <paramref name="terms"/>, and returns how much that is.</summary>
    public double Maximise(IReadOnlyList<(Variable Variable, double Coefficient)> terms)
    {
        Objective objective = _solver.Objective();
        objective.Clear();
        foreach (Variable flow in _flow.Values)
        {
            objective.SetCoefficient(flow, -Circling);
        }

        foreach ((Variable variable, double coefficient) in terms)
        {
            objective.SetCoefficient(variable, coefficient + objective.GetCoefficient(variable));
        }

        objective.SetMaximization();
        Solver.ResultStatus status = _solver.Solve();
        return status == Solver.ResultStatus.OPTIMAL
            ? terms.Sum(term => term.Coefficient * term.Variable.SolutionValue())
            : throw new InvalidOperationException($"The linear programme has no optimal solution: {status}");
    }

    // ---- What-ifs for the findings (BR-A11, A12): asked after the last objective, and undone again ------------------

    /// <summary>
    /// BR-A11: the share of its demand of <paramref name="item"/> a machine is offered: what would reach it if it took
    /// everything. Its products may vanish, its other inputs are unlimited, and what brings it its inputs is freed from
    /// sharing evenly, so it may take what its neighbours leave. Null when the solver can't tell.
    /// </summary>
    public double? OfferedShare(int machine, ItemId item) => WhatIf(
        () =>
        {
            Relax((_makes.GetValueOrDefault(machine) ?? []).Values);
            Relax((_takes.GetValueOrDefault(machine) ?? []).Where(input => input.Key != item).Select(input => input.Value));
            Free(_into[machine]);
        },
        [(_utilisation[machine], 1)]);

    /// <summary>
    /// BR-A11: the share of its production in one form (solids or fluids) its outputs can take: what could leave if its
    /// inputs were unlimited. Its other products may vanish, and what takes its products is freed from sharing evenly.
    /// </summary>
    public double? OutputRoom(int machine, IReadOnlySet<ItemId> products) => WhatIf(
        () =>
        {
            Relax((_takes.GetValueOrDefault(machine) ?? []).Values);
            Relax((_makes.GetValueOrDefault(machine) ?? []).Where(output => !products.Contains(output.Key)).Select(output => output.Value));
            Free(_outOf[machine]);
        },
        [(_utilisation[machine], 1)]);

    /// <summary>
    /// BR-A12: what is offered to a belt, lift or pipe: what would flow into it if it had no capacity and took
    /// everything, whatever lies beyond it. Up to <paramref name="atMost"/>.
    /// </summary>
    public double? OfferedInto(int conduit, double atMost) => WhatIf(
        () =>
        {
            Constraint capacity = _capacity[conduit];
            double upper = capacity.Ub();
            _restore!.Add(() => capacity.SetBounds(0, upper));
            capacity.SetBounds(0, atMost);
            foreach (Constraint conserved in _conserved.GetValueOrDefault(conduit) ?? [])
            {
                // What comes in may stay: in − out ≥ 0.
                _restore.Add(() => conserved.SetBounds(0, 0));
                conserved.SetBounds(0, double.PositiveInfinity);
            }

            Free(_into[conduit]);
        },
        [.. All(_into[conduit]).Select(variable => (variable, 1.0))]);

    /// <summary>Solves with some bounds relaxed, then puts every bound back as it was.</summary>
    private double? WhatIf(Action relax, IReadOnlyList<(Variable Variable, double Coefficient)> objective, Action? read = null)
    {
        _restore = [];
        try
        {
            relax();
            double most = Maximise(objective);
            read?.Invoke();
            return most;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        finally
        {
            _restore.Reverse();
            _restore.ForEach(undo => undo());
            _restore = null;
        }
    }

    /// <summary>Lets the left-hand side of each constraint fall below its bound: what it takes or makes may be less.</summary>
    private void Relax(IEnumerable<Constraint> constraints)
    {
        foreach (Constraint constraint in constraints)
        {
            (double lower, double upper) = (constraint.Lb(), constraint.Ub());
            _restore!.Add(() => constraint.SetBounds(lower, upper));
            constraint.SetBounds(double.NegativeInfinity, upper);
        }
    }

    /// <summary>The shares of these connections may leave their group's level, either way.</summary>
    private void Free(IEnumerable<int> edges)
    {
        foreach (Variable below in edges.SelectMany(edge => _belowOf.GetValueOrDefault(edge) ?? []))
        {
            (double lower, double upper) = (below.Lb(), below.Ub());
            _restore!.Add(() => below.SetBounds(lower, upper));
            below.SetBounds(double.NegativeInfinity, double.PositiveInfinity);
        }
    }

    /// <summary>Holds <paramref name="terms"/> at the most just found, for the objectives after it.</summary>
    public void Keep(IReadOnlyList<(Variable Variable, double Coefficient)> terms, double value)
    {
        Constraint kept = _solver.MakeConstraint(value - (1e-9 * Math.Max(1, Math.Abs(value))), double.PositiveInfinity);
        foreach ((Variable variable, double coefficient) in terms)
        {
            kept.SetCoefficient(variable, coefficient + kept.GetCoefficient(variable));
        }
    }

    /// <summary>
    /// After <see cref="Maximise"/> of <see cref="Push"/>: the shares to cap. A share is a candidate when letting it fall
    /// below its level would deliver more: the reduced cost of its fixed "below". A tier whose block costs delivery makes
    /// the shares above it candidates. But delivering more can also mean the items just go elsewhere (an overflow sink,
    /// another input of the same merger), and the game doesn't cap a share for that. So each candidate is confirmed: it
    /// is capped only when its far end really holds it below its level (<see cref="HeldByFarEnd"/>).
    /// </summary>
    public List<(int Group, int Edge)> SharesHoldingBack()
    {
        List<(int Group, int Edge)> candidates =
        [
            .. _uncappedShares.Where(share => share.Value.Below.ReducedCost() > CostsDelivery).Select(share => share.Key)
                .Concat(_blockedTiers.Values.Where(tier => tier.Blocked.ReducedCost() > CostsDelivery).SelectMany(tier => tier.Waiting))
                .Distinct()
        ];
        _solved = _flow.Values.Concat(_utilisation.Values).ToDictionary(variable => variable, variable => variable.SolutionValue());
        Dictionary<int, double> levels = candidates.Select(share => share.Group).Distinct()
            .ToDictionary(group => group, group => _levels[group].SolutionValue());
        // Batch after batch among the ones that rose, each with fewer near ends relaxed, until none more stays put.
        HashSet<(int Group, int Edge)> rose = [.. candidates];
        while (true)
        {
            HashSet<(int Group, int Edge)> still = Rising([.. rose], levels);
            if (still.Count == rose.Count)
            {
                break;
            }

            rose = still;
        }

        return [.. candidates.Where(share => !rose.Contains(share) || HeldByFarEnd(share, levels[share.Group]))];
    }

    /// <summary>
    /// All candidates in one what-if: every near end relaxed at once, each candidate at least at its level, the most of
    /// them together. One still at its level when its near end and all the others are relaxed is held by its far end:
    /// tested alone it would be held too, with less relaxed. The ones that rose may only have risen because the others
    /// were relaxed with them: they are asked again among themselves, and the last ones one by one. A big part has hundreds
    /// of candidates a round, and most are held: this asks a few times instead of hundreds.
    /// </summary>
    private HashSet<(int Group, int Edge)> Rising(List<(int Group, int Edge)> candidates, Dictionary<int, double> levels)
    {
        if (candidates.Count < 2)
        {
            return [.. candidates];
        }

        Dictionary<(int Group, int Edge), double> carried = [];
        HashSet<int> groups = [.. candidates.Select(share => share.Group)];
        WhatIf(
            () =>
            {
                double room = levels.Values.Max() + 1;
                foreach (int group in groups)
                {
                    RelaxNearEnd(_groups[group], room);
                    Fix(_levels[group], levels[group]);
                    FreezeOtherSide(_groups[group]);
                }

                foreach ((int Group, int Edge) share in candidates)
                {
                    Free([share.Edge]);
                }

                // Each candidate may rise above its level, not fall below it: share = level − below, with below ≤ 0.
                foreach ((int Group, int Edge) share in candidates)
                {
                    Variable below = _uncappedShares[share].Below;
                    (double lower, double upper) = (below.Lb(), below.Ub());
                    _restore!.Add(() => below.SetBounds(lower, upper));
                    below.SetBounds(double.NegativeInfinity, 0);
                }
            },
            [.. groups.SelectMany(group => NearSide(_groups[group]))],
            () => candidates.ForEach(share => carried[share] = _uncappedShares[share].Carried.Sum(variable => variable.SolutionValue())));

        return [.. candidates.Where(share => !carried.TryGetValue(share, out double most) || most > levels[share.Group] + (1e-6 * Math.Max(1, levels[share.Group])))];
    }

    /// <summary>
    /// Whether what lies at the far end of a share holds it at its level: for a splitter's output, what lies beyond can't
    /// take more even if the splitter had more to give; for an input, what feeds it can't offer more even if the node
    /// took everything. Asked with the near end giving or taking up to one item a minute more than the level: enough to
    /// tell, and it keeps a sink beyond from making the question unbounded.
    /// </summary>
    private bool HeldByFarEnd((int Group, int Edge) share, double level)
    {
        SharingGroup group = _groups[share.Group];
        List<(Variable Variable, double Coefficient)> nearSide = NearSide(group);
        double now = nearSide.Sum(term => term.Coefficient * _solved[term.Variable]);
        double most = WhatIf(
            () =>
            {
                RelaxNearEnd(group, level + 1);
                Fix(_levels[share.Group], level);
                FreezeOtherSide(group);
                Free([share.Edge]);
            },
            nearSide) ?? now;
        return most <= now + (1e-6 * Math.Max(1, now));
    }

    /// <summary>
    /// The near side of a group's node: what a splitter sends out, or what any other node takes in, of the items the
    /// group shares. Asked with the other side frozen (<see cref="FreezeOtherSide"/>), its rise is what the far ends of
    /// the shares could still take, or still offer.
    /// </summary>
    private List<(Variable Variable, double Coefficient)> NearSide(SharingGroup group) =>
    [
        .. (group.Inputs ? _into[group.Node] : _outOf[group.Node])
            .SelectMany(edge => _items.Items[edge].Where(item => group.Items?.Contains(item) != false).Select(item => (_flow[(edge, item)], 1.0)))
    ];

    /// <summary>
    /// Holds the other side of a group's node where the solution has it: a splitter's inputs, any other node's outputs, a
    /// machine's utilisation. Then items can't circle back through the node, and a node can't seem to take more just by
    /// passing on less.
    /// </summary>
    private void FreezeOtherSide(SharingGroup group)
    {
        if (_network.Nodes[group.Node] is FlowMachine)
        {
            Fix(_utilisation[group.Node], _solved[_utilisation[group.Node]]);
            return;
        }

        foreach (int edge in group.Inputs ? _outOf[group.Node] : _into[group.Node])
        {
            foreach (ItemId item in _items.Items[edge])
            {
                Fix(_flow[(edge, item)], _solved[_flow[(edge, item)]]);
            }
        }
    }

    /// <summary>
    /// Holds a variable where the solution has it, give or take a hair: exactly the solution's value can be just out of
    /// reach of the solver's own tolerances, and a what-if without an answer would be read wrongly.
    /// </summary>
    private void Fix(Variable variable, double value)
    {
        (double lower, double upper) = (variable.Lb(), variable.Ub());
        _restore!.Add(() => variable.SetBounds(lower, upper));
        double hair = 1e-7 * Math.Max(1, Math.Abs(value));
        variable.SetBounds(Math.Max(lower, value - hair), Math.Min(upper, value + hair));
    }

    /// <summary>
    /// Lets the near end of a group's shares give or take up to <paramref name="room"/> more: a splitter may send out more
    /// than comes in, a machine may take more than it uses, any other node may keep what comes in.
    /// </summary>
    private void RelaxNearEnd(SharingGroup group, double room)
    {
        if (!group.Inputs)
        {
            Widen(_conserved.GetValueOrDefault(group.Node) ?? [], -room, null);
        }
        else if (_network.Nodes[group.Node] is FlowMachine)
        {
            Widen((_takes.GetValueOrDefault(group.Node) ?? []).Where(input => group.Items?.Contains(input.Key) != false).Select(input => input.Value), null, room);
        }
        else
        {
            Widen(_conserved.GetValueOrDefault(group.Node) ?? [], null, room);
        }
    }

    /// <summary>Sets new bounds on these constraints for a what-if; null keeps that bound.</summary>
    private void Widen(IEnumerable<Constraint> constraints, double? lower, double? upper)
    {
        foreach (Constraint constraint in constraints)
        {
            (double was, double wasUpper) = (constraint.Lb(), constraint.Ub());
            _restore!.Add(() => constraint.SetBounds(was, wasUpper));
            constraint.SetBounds(lower ?? was, upper ?? wasUpper);
        }
    }

    public LinearFlow Result() => new(
        _network,
        [
            .. Enumerable.Range(0, _network.Edges.Count).Select(edge => (IReadOnlyDictionary<ItemId, double>)_items.Items[edge]
                .Select(item => (Item: item, Rate: _flow[(edge, item)].SolutionValue()))
                .Where(entry => entry.Rate > SteadyState.Tolerance)
                .ToDictionary(entry => entry.Item, entry => entry.Rate))
        ],
        _network.Nodes.Select((node, index) => (Node: node, Index: index))
            .Where(entry => entry.Node is FlowMachine)
            .ToDictionary(
                entry => entry.Node.Id,
                entry => _utilisation.TryGetValue(entry.Index, out Variable? u) ? Math.Clamp(u.SolutionValue(), 0, 1) : 0));

    public void Dispose() => _solver.Dispose();

    private void Build(int node, List<int> into, List<int> outOf)
    {
        switch (_network.Nodes[node])
        {
            case FlowMachine { Demand.Count: 0, Production.Count: 0 }:
                // No recipe the analysis knows: it asks and makes nothing (BR-W10).
                break;
            case FlowMachine machine:
                Variable u = _solver.MakeNumVar(0, 1, $"u{node}");
                _utilisation[node] = u;
                foreach ((ItemId item, double demand) in machine.Demand.Where(demand => demand.Value > 0))
                {
                    // A stocked input counts as supplied: it still takes what its belts bring, up to its demand.
                    bool stocked = machine.Stocked?.Contains(item) == true;
                    Constraint takes = _solver.MakeConstraint(stocked ? double.NegativeInfinity : 0, 0);
                    Sum(takes, Over(into, item));
                    takes.SetCoefficient(u, -demand);
                    Register(_takes, node, item, takes);
                }

                foreach ((ItemId item, double production) in machine.Production.Where(product => product.Value > 0))
                {
                    // A product with no line of its form to leave by holds the machine at 0 (BR-A3).
                    Constraint makes = _solver.MakeConstraint(0, 0);
                    Sum(makes, Over(outOf, item));
                    makes.SetCoefficient(u, -production);
                    Register(_makes, node, item, makes);
                }

                break;
            case FlowConduit or FlowSplitter or FlowMerger:
                HashSet<ItemId> passing = [.. into.Concat(outOf).SelectMany(edge => _items.Items[edge])];
                foreach (ItemId item in passing)
                {
                    Constraint conserved = _solver.MakeConstraint(0, 0);
                    Sum(conserved, Over(into, item));
                    Sum(conserved, Over(outOf, item), -1);
                    if (!_conserved.TryGetValue(node, out List<Constraint>? ofNode))
                    {
                        ofNode = [];
                        _conserved[node] = ofNode;
                    }

                    ofNode.Add(conserved);
                }

                if (_network.Nodes[node] is FlowConduit { Capacity: < double.PositiveInfinity } conduit)
                {
                    Constraint capacity = _solver.MakeConstraint(0, conduit.Capacity);
                    Sum(capacity, All(into));
                    _capacity[node] = capacity;
                }

                break;
            case FlowProvider provider:
                Constraint offer = _solver.MakeConstraint(0, provider.Offer);
                foreach (Variable delivered in Over(outOf, provider.Item))
                {
                    offer.SetCoefficient(delivered, 1);
                    _push.Add((delivered, 1));
                }

                break;
            case FlowSink { BurnRate: { } burnRate, Burns: { Count: > 0 } fuels }:
                // BR-A16: a drone port whose rate is known takes no more of its fuel than its drones burn.
                Burning(into, fuels, burnRate);
                break;
            case FlowUnknownSource source:
                if (source is { BurnRate: { } burns, Burns: { Count: > 0 } fuel })
                {
                    Burning(into, fuel, burns);
                }

                Constraint asked = _solver.MakeConstraint(0, _items.Asked.GetValueOrDefault(node));
                foreach (Variable delivered in All(outOf))
                {
                    asked.SetCoefficient(delivered, 1);
                    _push.Add((delivered, 1));
                }

                break;
        }
    }

    private void Share(int group, SharingGroup sharing, IReadOnlySet<(int Group, int Edge)> capped)
    {
        Variable level = _solver.MakeNumVar(0, double.PositiveInfinity, $"t{group}");
        _levels[group] = level;
        foreach (int edge in sharing.Edges)
        {
            List<Variable> carried = [.. sharing.Items is { } shared ? shared.SelectMany(item => Over([edge], item)) : All([edge])];
            // share − level + below = 0: a share that isn't capped may not fall below the level (below fixed at 0),
            // a capped one may. The reduced cost of a fixed "below" is what letting it fall would deliver.
            bool isCapped = capped.Contains((group, edge));
            Variable below = _solver.MakeNumVar(0, isCapped ? double.PositiveInfinity : 0, $"s{group}:{edge}");
            Constraint share = _solver.MakeConstraint(0, 0);
            Sum(share, carried);
            share.SetCoefficient(level, -1);
            share.SetCoefficient(below, 1);
            if (!_belowOf.TryGetValue(edge, out List<Variable>? belows))
            {
                belows = [];
                _belowOf[edge] = belows;
            }

            belows.Add(below);
            if (isCapped)
            {
                AddCapped(sharing.Tier, carried);
            }
            else
            {
                _uncappedShares[(group, edge)] = (below, carried, sharing.Tier);
            }
        }
    }

    /// <summary>A tier gets nothing while any share of the groups above it isn't capped.</summary>
    private void Wait(int group, IReadOnlyList<SharingGroup> groups, IReadOnlySet<(int Group, int Edge)> capped)
    {
        SharingGroup sharing = groups[group];
        List<(int Group, int Edge)> waiting =
        [
            .. (sharing.Above ?? []).SelectMany(above => groups[above].Edges.Select(edge => (above, edge))).Where(share => !capped.Contains(share))
        ];
        if (waiting.Count == 0)
        {
            return;
        }

        // Through one variable fixed at 0, rather than the flows themselves: their own bounds of 0 would take the dual
        // value, and this variable's reduced cost says exactly what lifting the block would deliver.
        Variable blocked = _solver.MakeNumVar(0, 0, $"b{group}");
        Constraint tier = _solver.MakeConstraint(0, 0);
        Sum(tier, sharing.Edges.SelectMany(edge => sharing.Items is { } shared ? shared.SelectMany(item => Over([edge], item)) : All([edge])));
        tier.SetCoefficient(blocked, -1);
        _blockedTiers[group] = (blocked, waiting);
    }

    private void AddCapped(int tier, List<Variable> carried)
    {
        if (!_cappedShares.TryGetValue(tier, out List<(Variable Variable, double Coefficient)>? shares))
        {
            shares = [];
            _cappedShares[tier] = shares;
        }

        shares.AddRange(carried.Select(variable => (variable, 1.0)));
    }

    private static void Register(Dictionary<int, Dictionary<ItemId, Constraint>> per, int node, ItemId item, Constraint constraint)
    {
        if (!per.TryGetValue(node, out Dictionary<ItemId, Constraint>? ofNode))
        {
            ofNode = [];
            per[node] = ofNode;
        }

        ofNode[item] = constraint;
    }

    private void Burning(List<int> into, IReadOnlySet<ItemId> fuels, double perMinute)
    {
        Constraint burns = _solver.MakeConstraint(0, perMinute);
        Sum(burns, fuels.SelectMany(fuel => Over(into, fuel)));
    }

    private IEnumerable<Variable> Over(IEnumerable<int> edges, ItemId item) =>
        edges.Where(edge => _flow.ContainsKey((edge, item))).Select(edge => _flow[(edge, item)]);

    private IEnumerable<Variable> All(IEnumerable<int> edges) =>
        edges.SelectMany(edge => _items.Items[edge].Select(item => _flow[(edge, item)]));

    private static void Sum(Constraint constraint, IEnumerable<Variable> variables, double coefficient = 1)
    {
        foreach (Variable variable in variables)
        {
            constraint.SetCoefficient(variable, coefficient);
        }
    }

    private List<int>[] Lists(Func<(int From, int To), int> end)
    {
        List<int>[] lists = [.. _network.Nodes.Select(_ => new List<int>())];
        for (int edge = 0; edge < _network.Edges.Count; edge++)
        {
            lists[end(_network.Edges[edge])].Add(edge);
        }

        return lists;
    }
}
