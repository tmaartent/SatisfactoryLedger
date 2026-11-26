namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>
/// The trace's causes in the ledger's terms (BR-A20, BR-A21; decided 2026-11-22): the one place they're written. The
/// trace tools' descriptions are built from these constants, each start's <c>causeTerm</c> comes from them, and the
/// Assistant and the web app read that term instead of keeping words of their own.
/// </summary>
internal static class TraceCauses
{
    public const string TooFewProducers = "too few producers";
    public const string Bottleneck = "bottleneck";
    public const string SourceAtLimit = "source at its limit";
    public const string TakenByOthers = "taken by others";
    public const string NothingBringsIt = "nothing brings it";
    public const string WithoutARecipe = "without a recipe";
    public const string UnknownSupply = "unknown supply";
    public const string ProducerOutputBlocked = "producer output blocked";
    public const string TooFewConsumers = "too few consumers";
    public const string SuppliedByOthers = "supplied by others";
    public const string ConsumerStarved = "consumer starved";
    public const string StorageFull = "storage full";
    public const string NowhereToGo = "nowhere to go";
    public const string NoDroneRefuelsHere = "no drone refuels here";
    public const string Unclear = "unclear";

    /// <summary>The API's cause codes, as <c>TraceCause</c> is written in JSON.</summary>
    private static readonly Dictionary<string, string> Terms = new(StringComparer.Ordinal)
    {
        ["tooFewProducers"] = TooFewProducers,
        ["beltFull"] = Bottleneck,
        ["sourceAtLimit"] = SourceAtLimit,
        ["takenByOthers"] = TakenByOthers,
        ["nothingBringsIt"] = NothingBringsIt,
        ["noRecipe"] = WithoutARecipe,
        ["unknownSupply"] = UnknownSupply,
        ["producerOutputBlocked"] = ProducerOutputBlocked,
        ["tooFewConsumers"] = TooFewConsumers,
        ["suppliedByOthers"] = SuppliedByOthers,
        ["consumerStarved"] = ConsumerStarved,
        ["storageFull"] = StorageFull,
        ["nowhereToGo"] = NowhereToGo,
        ["noDroneRefuels"] = NoDroneRefuelsHere,
        ["unclear"] = Unclear,
    };

    /// <summary>A cause's term; a cause the MCP server doesn't know yet keeps its code.</summary>
    public static string TermOf(string cause) => Terms.GetValueOrDefault(cause, cause);
}
