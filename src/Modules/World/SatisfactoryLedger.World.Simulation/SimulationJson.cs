using System.Text.Json;
using System.Text.Json.Serialization;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>The JSON settings of the documents both services read and write: camelCase, enums as camelCase strings.</summary>
public static class SimulationJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}