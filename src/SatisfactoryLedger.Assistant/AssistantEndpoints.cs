using Microsoft.Extensions.AI;

using SatisfactoryLedger.Assistant.Explaining;
using SatisfactoryLedger.Assistant.LedgerTools;
using SatisfactoryLedger.Assistant.Models;

namespace SatisfactoryLedger.Assistant;

/// <summary>The Assistant's HTTP endpoints. The web app reaches them through its dev server's proxy, as it does the API.</summary>
internal static class AssistantEndpoints
{
    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder assistant = app.MapGroup("/api/v1/assistant");

        assistant.MapGet("/models", (ChatModels models, IServiceProvider services) =>
            models.Keys.Select(key => new ChatModelResponse(
                key,
                services.GetRequiredKeyedService<IChatClient>(key).GetService<ChatClientMetadata>()?.DefaultModelId ?? key)));

        assistant.MapPost("/explanations", ExplainAsync);
        return app;
    }

    /// <summary>
    /// Explains a finding with the model asked for. A model it doesn't have is a 400 naming the ones it has; what the
    /// ledger's tools can't answer is a 422 with their message.
    /// </summary>
    private static async Task<IResult> ExplainAsync(
        ExplanationRequest request, ChatModels models, IServiceProvider services, FindingExplainer explainer, CancellationToken cancellationToken)
    {
        string key = request.Model ?? models.Default;
        if (!models.Keys.Contains(key, StringComparer.Ordinal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["model"] = [$"There's no model \"{key}\". The models are: {string.Join(", ", models.Keys)}."],
            });
        }

        try
        {
            return Results.Ok(await explainer.ExplainAsync(request, services.GetRequiredKeyedService<IChatClient>(key), cancellationToken));
        }
        catch (LedgerToolException unanswered)
        {
            return Results.Problem(unanswered.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "The ledger couldn't answer");
        }
    }
}
