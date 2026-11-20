using System.Net;
using System.Text;
using System.Text.Json;

namespace SatisfactoryLedger.Mcp.Tests;

/// <summary>The ledger's API as the MCP server sees it: a JSON answer per path and query, 404 for anything else.</summary>
internal sealed class FakeLedgerApi
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Json)> _answers = new(StringComparer.Ordinal);

    /// <summary>Every path and query asked, in order.</summary>
    public List<string> Asked { get; } = [];

    public FakeLedgerApi Answer(string pathAndQuery, object body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _answers[pathAndQuery] = (status, JsonSerializer.Serialize(body, JsonSerializerOptions.Web));
        return this;
    }

    /// <summary>A new handler each time: the client factory disposes the handlers it's given.</summary>
    public HttpMessageHandler Handler() => new AnsweringHandler(this);

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        string asked = request.RequestUri!.PathAndQuery;
        Asked.Add(asked);
        return _answers.TryGetValue(asked, out var answer)
            ? new HttpResponseMessage(answer.Status) { Content = new StringContent(answer.Json, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private sealed class AnsweringHandler(FakeLedgerApi api) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(api.Respond(request));
    }
}
