using Microsoft.Extensions.AI;

namespace SatisfactoryLedger.Assistant.Tests;

/// <summary>A chat model that always gives the same answer, and keeps what it was sent.</summary>
internal sealed class FakeChatClient(string answer) : IChatClient
{
    public const string Model = "fake-model";

    /// <summary>The messages of the last call.</summary>
    public List<ChatMessage> Messages { get; } = [];

    public int Calls { get; private set; }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls++;
        Messages.Clear();
        Messages.AddRange(messages);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)) { ModelId = Model });
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("fake", defaultModelId: Model) : null;

    public void Dispose()
    {
    }
}
