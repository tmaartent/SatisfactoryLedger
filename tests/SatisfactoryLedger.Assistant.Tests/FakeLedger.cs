using ModelContextProtocol;

namespace SatisfactoryLedger.Assistant.Tests;

/// <summary>What the fake ledger tools answer, per tool: a JSON answer or an error; and what they were asked.</summary>
internal sealed class FakeLedger
{
    private readonly Dictionary<string, string> _answers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);

    /// <summary>Every call, in order: the tool and its arguments.</summary>
    public List<(string Tool, string Building, string? Recipe, string? SnapshotId)> Asked { get; } = [];

    public FakeLedger Answer(string tool, string json)
    {
        _answers[tool] = json;
        return this;
    }

    /// <summary>The tool reports an error, as the real tools do with an <see cref="McpException"/>.</summary>
    public FakeLedger Fail(string tool, string message)
    {
        _errors[tool] = message;
        return this;
    }

    public string Respond(string tool, string building, string? recipe, string? snapshotId)
    {
        Asked.Add((tool, building, recipe, snapshotId));
        return _errors.TryGetValue(tool, out string? error) ? throw new McpException(error) : _answers[tool];
    }
}
