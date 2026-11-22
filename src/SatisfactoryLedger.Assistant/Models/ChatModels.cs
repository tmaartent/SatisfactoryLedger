namespace SatisfactoryLedger.Assistant.Models;

/// <summary>
/// The chat models the Assistant may use, by the key their <c>IChatClient</c> is registered under; the first is the
/// default. Claude is only there when an API key is configured.
/// </summary>
internal sealed class ChatModels(IReadOnlyList<string> keys)
{
    /// <summary>The local model, on Ollama.</summary>
    public const string Local = "local";

    public const string Claude = "claude";

    public IReadOnlyList<string> Keys { get; } = keys;

    public string Default => Keys[0];
}
