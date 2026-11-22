using Anthropic;
using Anthropic.Core;

using Microsoft.Extensions.AI;

namespace SatisfactoryLedger.Assistant.Models;

/// <summary>
/// The chat models, each an <see cref="IChatClient"/> under its key (ADR-0030): the local model on Ollama always, and
/// Claude when <c>Anthropic:ApiKey</c> is set. Tests register fakes instead.
/// </summary>
internal static class ChatModelSetup
{
    /// <summary>The model's name in the AppHost: its connection string names Ollama's address and the model.</summary>
    private const string LocalConnection = "chat";

    private const string DefaultClaudeModel = "claude-sonnet-5-5";

    public static IHostApplicationBuilder AddChatModels(this IHostApplicationBuilder builder)
    {
        // In development, the dashboard's traces show each prompt and answer: the way to see what the model was given.
        bool showPrompts = builder.Environment.IsDevelopment();
        List<string> keys = [ChatModels.Local];

        builder.AddOllamaApiClient(LocalConnection)
            .AddKeyedChatClient(ChatModels.Local, telemetry => telemetry.EnableSensitiveData = showPrompts);

        // ServiceDefaults gives every HttpClient retries and a 10-second attempt timeout. A model's answer can take longer,
        // its first while the model loads, and retrying one only asks it all again. The client is named after the connection.
#pragma warning disable EXTEXP0001 // Experimental in 10.10; it's what removing the defaults from one client is for.
        builder.Services.AddHttpClient($"{LocalConnection}_httpClient").RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        if (builder.Configuration["Anthropic:ApiKey"] is { Length: > 0 } apiKey)
        {
            string model = builder.Configuration["Anthropic:Model"] ?? DefaultClaudeModel;
            builder.Services
                .AddKeyedChatClient(ChatModels.Claude, _ => new AnthropicClient(new ClientOptions { ApiKey = apiKey }).AsIChatClient(model))
                .UseOpenTelemetry(configure: telemetry => telemetry.EnableSensitiveData = showPrompts);
            keys.Add(ChatModels.Claude);
        }

        builder.Services.AddSingleton(new ChatModels(keys));
        return builder;
    }
}
