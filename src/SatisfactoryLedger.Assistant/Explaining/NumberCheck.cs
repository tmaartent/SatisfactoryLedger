using System.Text.Json;
using System.Text.RegularExpressions;

namespace SatisfactoryLedger.Assistant.Explaining;

/// <summary>
/// The model explains in words, the list beside it gives the numbers (asked 2026-11-22): a small model copied the right
/// numbers but pinned them on the wrong things, "9 units of Wire" for 9 machines. Finds the numbers it wrote anyway,
/// so the player sees which to doubt.
/// </summary>
internal static partial class NumberCheck
{
    /// <summary>
    /// The numbers in an explanation outside the names the facts give, as written. "Conveyor Belt Mk.6" is a name, so
    /// its 6 isn't a number the model wrote; "6 machines" is. Nor is a tier mark's, as "Mk.5" (asked 2026-11-24): the
    /// model names the belt to rebuild as, which the facts don't give by name.
    /// </summary>
    public static IReadOnlyList<string> Unsupported(string explanation, JsonElement facts)
    {
        List<string> names = [];
        Collect(facts, names);
        string words = names.Distinct(StringComparer.Ordinal)
            .OrderByDescending(name => name.Length)
            .Aggregate(explanation, (text, name) => text.Replace(name, " ", StringComparison.Ordinal));
        return [.. Numbers().Matches(TierMarks().Replace(words, " ")).Select(match => match.Value)];
    }

    private static void Collect(JsonElement element, List<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String when Numbers().IsMatch(element.GetString()!):
                names.Add(element.GetString()!);
                break;
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    Collect(property.Value, names);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Collect(item, names);
                }

                break;
        }
    }

    /// <summary>A tier in a building's name, as the game writes it: "Mk.5", also "Mk 5" or "Mk. 5".</summary>
    [GeneratedRegex(@"\bMk\.?\s?\d+")]
    private static partial Regex TierMarks();

    /// <summary>Digits, with decimals after a point. A full stop ending a sentence isn't one: no digit follows it.</summary>
    [GeneratedRegex(@"\d+(?:\.\d+)?")]
    private static partial Regex Numbers();
}
