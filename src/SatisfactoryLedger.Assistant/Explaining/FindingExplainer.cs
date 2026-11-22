using System.Text.Json;

using Microsoft.Extensions.AI;

using SatisfactoryLedger.Assistant.LedgerTools;

namespace SatisfactoryLedger.Assistant.Explaining;

/// <summary>
/// Explains a finding (AI enhancement 3): code gathers the facts with the trace that fits the finding, the model puts
/// each start in words in a call of its own, and code checks the text for numbers.
/// </summary>
internal sealed class FindingExplainer(LedgerToolsClient tools)
{
    /// <summary>Low, not zero: the same facts should read much the same each time, without the model repeating itself.</summary>
    private const float Temperature = 0.2f;

    /// <summary>Three sentences, with room to spare.</summary>
    private const int MaxOutputTokens = 250;

    public async Task<ExplanationResponse> ExplainAsync(ExplanationRequest request, IChatClient model, CancellationToken cancellationToken)
    {
        string tool = request.Limit == HeldBack.Starved ? "trace_upstream" : "trace_downstream";
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal)
        {
            ["building"] = request.Building,
            ["snapshotId"] = request.SnapshotId,
        };
        if (request.Recipe is not null)
        {
            arguments["recipe"] = request.Recipe;
        }

        ToolCall facts = await tools.CallAsync(tool, arguments, cancellationToken);
        var factsResponse = new FactsResponse(facts.Tool, facts.Arguments, facts.Answer);

        // Nothing held back, or no such machines: the trace says so itself, and a model could only add to it.
        if (facts.Answer.GetProperty("traced").GetInt32() == 0 && facts.Answer.TryGetProperty("note", out JsonElement note))
        {
            return new ExplanationResponse(note.GetString()!, null, factsResponse, []);
        }

        // Held back, but no start found: a model given no cause makes one up, so it isn't asked.
        if (facts.Answer.GetProperty("starts").GetArrayLength() == 0)
        {
            return new ExplanationResponse(
                "The ledger traced these machines but found no start for what holds them back, so there's nothing to explain yet.",
                null,
                factsResponse,
                []);
        }

        // One start at a time, in the trace's order: two in one prompt were merged into a cause neither gives.
        List<string> paragraphs = [];
        string? modelId = null;
        for (int start = 0; start < facts.Answer.GetProperty("starts").GetArrayLength(); start++)
        {
            ChatResponse response = await model.GetResponseAsync(
                ExplanationPrompt.For(request.Limit, facts, start),
                new ChatOptions { Temperature = Temperature, MaxOutputTokens = MaxOutputTokens },
                cancellationToken);
            paragraphs.Add(response.Text.Trim());
            modelId ??= response.ModelId;
        }

        string text = string.Join("\n\n", paragraphs);
        modelId ??= model.GetService<ChatClientMetadata>()?.DefaultModelId;
        return new ExplanationResponse(text, modelId, factsResponse, NumberCheck.Unsupported(text, facts.Answer));
    }
}
