using System.ComponentModel;

using ModelContextProtocol.Server;

namespace SatisfactoryLedger.Assistant.Tests;

/// <summary>The ledger's trace tools as the MCP server names them, answering what <see cref="FakeLedger"/> holds.</summary>
[McpServerToolType]
internal sealed class FakeLedgerTools(FakeLedger ledger)
{
    public const string UpstreamDescription = "Where the shortage of starved machines starts: tooFewProducers (they run at full speed), beltFull, takenByOthers.";
    public const string DownstreamDescription = "Where the blockage of output-blocked machines starts: tooFewConsumers, beltFull, suppliedByOthers.";

    [McpServerTool(Name = "trace_upstream", ReadOnly = true)]
    [Description(UpstreamDescription)]
    public string TraceUpstream(string building, string? recipe = null, string? snapshotId = null) =>
        ledger.Respond("trace_upstream", building, recipe, snapshotId);

    [McpServerTool(Name = "trace_downstream", ReadOnly = true)]
    [Description(DownstreamDescription)]
    public string TraceDownstream(string building, string? recipe = null, string? snapshotId = null) =>
        ledger.Respond("trace_downstream", building, recipe, snapshotId);
}
