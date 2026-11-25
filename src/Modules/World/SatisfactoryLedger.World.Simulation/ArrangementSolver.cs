using Google.OrTools.Sat;

using SatisfactoryLedger.World.Domain.Arrangement;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// The optimal arrangement (decided 2026-11-12), solved with OR-Tools' CP-SAT:
/// <list type="bullet">
/// <item>Variables: each factory's lowest corner, inside the area.</item>
/// <item>No two take the same space: every factory, and every fixed place that blocks (a station, a drone port; not a
/// node, which factories may stand over), is a rectangle widened by the gaps, and none of them overlap
/// (<c>NoOverlap2D</c>). Blocking places that overlap each other are merged into one first.</item>
/// <item>Objective: per link, the belts it needs times the distance along the grid between the centres at the link's
/// weight (in percent), plus, to keep
/// them together, the width and depth of the rectangle around all factories times its weight; then, to break ties, how
/// far the factories move, so one that saves nothing by moving stays.</item>
/// </list>
/// Centres are doubled to stay whole numbers: twice the centre is <c>2x + width</c>.
/// </summary>
public static class ArrangementSolver
{
    /// <param name="avoid">Earlier placements of the same problem, not to be found again: in each, at least one factory
    /// stands elsewhere (step 101: placing again after belts were left unrouted).</param>
    /// <param name="seed">The solver's random seed: another for each placement, so its search goes another way.</param>
    public static ArrangementProposal Solve(
        ArrangementProblem problem,
        TimeSpan timeLimit,
        IReadOnlyList<IReadOnlyDictionary<string, GridRectangle>>? avoid = null,
        int seed = 0)
    {
        ArgumentNullException.ThrowIfNull(problem);
        var model = new CpModel();
        GridRectangle area = problem.Area;
        NoOverlap2dConstraint apart = model.AddNoOverlap2D();

        Dictionary<string, (LinearExpr X, LinearExpr Y)> doubleCentres = [];
        Dictionary<string, (IntVar X, IntVar Y)> corners = [];
        List<LinearExpr> moves = [];
        IntVar left = model.NewIntVar(area.X, area.X + area.Width, "left");
        IntVar right = model.NewIntVar(area.X, area.X + area.Width, "right");
        IntVar bottom = model.NewIntVar(area.Y, area.Y + area.Depth, "bottom");
        IntVar top = model.NewIntVar(area.Y, area.Y + area.Depth, "top");
        foreach (ArrangedFactory factory in problem.Factories)
        {
            GridRectangle now = factory.Now;
            IntVar x = model.NewIntVar(area.X, area.X + area.Width - now.Width, $"{factory.Id}.x");
            IntVar y = model.NewIntVar(area.Y, area.Y + area.Depth - now.Depth, $"{factory.Id}.y");
            apart.AddRectangle(
                model.NewFixedSizeIntervalVar(x, now.Width + problem.GapX, $"{factory.Id}.xs"),
                model.NewFixedSizeIntervalVar(y, now.Depth + problem.GapY, $"{factory.Id}.ys"));
            corners[factory.Id] = (x, y);
            model.Add(left <= x);
            model.Add(right >= x + now.Width);
            model.Add(bottom <= y);
            model.Add(top >= y + now.Depth);
            doubleCentres[factory.Id] = ((2 * x) + now.Width, (2 * y) + now.Depth);
            moves.Add(Absolute(model, x - now.X, Span(area)));
            moves.Add(Absolute(model, y - now.Y, Span(area)));
            if (Within(now, area))
            {
                model.AddHint(x, now.X);
                model.AddHint(y, now.Y);
            }
        }

        foreach (GridRectangle space in Merged(problem.Fixed.Where(place => place.Blocks).Select(place => Widened(place.Space, problem.GapX, problem.GapY))))
        {
            apart.AddRectangle(
                model.NewFixedSizeIntervalVar(model.NewConstant(space.X), space.Width, "fixed.xs"),
                model.NewFixedSizeIntervalVar(model.NewConstant(space.Y), space.Depth, "fixed.ys"));
        }

        foreach (FixedPlace place in problem.Fixed)
        {
            doubleCentres[place.Id] = (LinearExpr.Constant(place.Space.DoubleCentreX), LinearExpr.Constant(place.Space.DoubleCentreY));
        }

        List<LinearExpr> belts = [];
        foreach (ArrangementLink link in problem.Links.Where(link => link.BeltsAndPipes > 0))
        {
            (LinearExpr fromX, LinearExpr fromY) = doubleCentres[link.From];
            (LinearExpr toX, LinearExpr toY) = doubleCentres[link.To];
            belts.Add(link.BeltsAndPipes * link.WeightPercent * (Absolute(model, fromX - toX, 2 * Span(area)) + Absolute(model, fromY - toY, 2 * Span(area))));
        }

        // The belts are in doubled distances and in percent, so the spread is too.
        LinearExpr spread = 200L * problem.Together * (right - left + top - bottom);

        // Belt work first: one cell of it outweighs every factory moving across the whole area.
        long movesAtMost = (2L * Span(area) * problem.Factories.Count) + 1;
        // Not an earlier placement: some factory's corner lies a square or more left, right, below or above where it stood.
        foreach (IReadOnlyDictionary<string, GridRectangle> earlier in avoid ?? [])
        {
            List<ILiteral> elsewhere = [];
            foreach ((string id, (IntVar x, IntVar y)) in corners)
            {
                if (earlier.GetValueOrDefault(id) is not { } was)
                {
                    continue;
                }

                foreach (BoundedLinearExpression moved in new[] { x <= was.X - 1, x >= was.X + 1, y <= was.Y - 1, y >= was.Y + 1 })
                {
                    BoolVar chosen = model.NewBoolVar(string.Empty);
                    model.Add(moved).OnlyEnforceIf(chosen);
                    elsewhere.Add(chosen);
                }
            }

            model.AddBoolOr(elsewhere);
        }

        model.Minimize((movesAtMost * (LinearExpr.Sum(belts) + spread)) + LinearExpr.Sum(moves));

        var solver = new CpSolver
        {
            StringParameters = FormattableString.Invariant($"max_time_in_seconds:{timeLimit.TotalSeconds:0.###} num_workers:8 random_seed:{seed}"),
        };
        CpSolverStatus status = solver.Solve(model);
        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
        {
            // No layout: nothing is placed, not even where it stands now, which would look like a proposal.
            return new ArrangementProposal(new Dictionary<string, GridRectangle>(), problem.CostNow, problem.CostNow, ArrangementStatus.NotFound);
        }

        Dictionary<string, GridRectangle> places = problem.Factories.ToDictionary(
            factory => factory.Id,
            factory => factory.Now.At((int)solver.Value(corners[factory.Id].X), (int)solver.Value(corners[factory.Id].Y)));
        return new ArrangementProposal(
            places,
            problem.CostOf(places),
            problem.CostNow,
            status == CpSolverStatus.Optimal ? ArrangementStatus.Optimal : ArrangementStatus.Feasible);
    }

    /// <summary>The farthest anything can be from anything along one axis, with room for factories outside the area now.</summary>
    private static long Span(GridRectangle area) => 4L * (Math.Abs((long)area.X) + Math.Abs((long)area.Y) + area.Width + area.Depth);

    /// <summary>|expression|, as a variable the minimisation keeps at it: at least the expression and its negative.</summary>
    private static IntVar Absolute(CpModel model, LinearExpr expression, long atMost)
    {
        IntVar absolute = model.NewIntVar(0, atMost, string.Empty);
        model.Add(absolute >= expression);
        model.Add(absolute >= -expression);
        return absolute;
    }

    private static bool Within(GridRectangle place, GridRectangle area) =>
        place.X >= area.X && place.Y >= area.Y && place.X + place.Width <= area.X + area.Width && place.Y + place.Depth <= area.Y + area.Depth;

    /// <summary>A rectangle with the gaps on its far sides: two widened rectangles that don't overlap keep the gaps.</summary>
    private static GridRectangle Widened(GridRectangle space, int gapX, int gapY) => new(space.X, space.Y, space.Width + gapX, space.Depth + gapY);

    /// <summary>Overlapping rectangles merged into the rectangle around them, until none overlap.</summary>
    private static List<GridRectangle> Merged(IEnumerable<GridRectangle> spaces)
    {
        List<GridRectangle> merged = [.. spaces];
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < merged.Count && !changed; i++)
            {
                for (int j = i + 1; j < merged.Count && !changed; j++)
                {
                    if (Overlap(merged[i], merged[j]))
                    {
                        merged[i] = Around(merged[i], merged[j]);
                        merged.RemoveAt(j);
                        changed = true;
                    }
                }
            }
        }

        return merged;
    }

    private static bool Overlap(GridRectangle a, GridRectangle b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Depth && b.Y < a.Y + a.Depth;

    private static GridRectangle Around(GridRectangle a, GridRectangle b)
    {
        int x = Math.Min(a.X, b.X);
        int y = Math.Min(a.Y, b.Y);
        return new GridRectangle(x, y, Math.Max(a.X + a.Width, b.X + b.Width) - x, Math.Max(a.Y + a.Depth, b.Y + b.Depth) - y);
    }
}
