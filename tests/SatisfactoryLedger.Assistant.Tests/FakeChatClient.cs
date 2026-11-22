using Microsoft.Extensions.AI;

namespace SatisfactoryLedger.Assistant.Tests;

/// <summary>A chat model that always gives the same answer, and keeps what it was sent.</summary>
internal sealed class FakeChatClient(string answer) : IChatClient
{
    public const string Model = "fake-model";

    /// <summary>The messages of the last call.</summary>
    public List<ChatMessage> Messages { get; } = [];

    /// <summary>The question of each call, in order.</summary>
    public List<string> Questions { get; } = [];

    public int Calls => Questions.Count;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Messages.Clear();
        Messages.AddRange(messages);
        Questions.Add(Messages.Last(message => message.Role == ChatRole.User).Text);
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
