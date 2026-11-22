namespace SatisfactoryLedger.Assistant.Models;

/// <summary>A model the Assistant may use: the key to ask for it by, and the model it runs (<c>qwen2.5:7b</c>).</summary>
internal sealed record ChatModelResponse(string Key, string Model);
