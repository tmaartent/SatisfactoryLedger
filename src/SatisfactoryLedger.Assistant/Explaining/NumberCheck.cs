using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SatisfactoryLedger.Assistant.Explaining;

/// <summary>
/// The model explains, the solver computes: every number in an explanation should come from the facts it was given.
/// Finds the ones that don't, so the player sees which to doubt.
/// </summary>
internal static partial class NumberCheck
{
    /// <summary>Full speed: a machine at 100% needs no fact to say so.</summary>
    private const double FullSpeed = 100;

    /// <summary>
    /// The numbers in an explanation that no number in the facts gives, as written. A fact counts when it rounds to the
    /// number at the decimals written, or, for a whole number, rounds to it up or down (22.5 is "about 22" or "23").
    /// Numbers inside the facts' strings count too: names (Mk.2), ids, notes. A sign is ignored: a position may be
    /// written as "-3" or "3 south".
    /// </summary>
    public static IReadOnlyList<string> Unsupported(string explanation, JsonElement facts)
    {
        List<double> known = [FullSpeed];
        Collect(facts, known);
        return [.. Numbers().Matches(explanation).Select(match => match.Value).Where(written => !known.Any(fact => Gives(fact, written)))];
    }

    private static bool Gives(double fact, string written)
    {
        double value = double.Parse(written, CultureInfo.InvariantCulture);
        int decimals = written.Contains('.', StringComparison.Ordinal) ? written.Length - written.IndexOf('.', StringComparison.Ordinal) - 1 : 0;
        double magnitude = Math.Abs(fact);
        return Math.Round(magnitude, decimals, MidpointRounding.AwayFromZero) == value
            || (decimals == 0 && (Math.Floor(magnitude) == value || Math.Ceiling(magnitude) == value));
    }

    private static void Collect(JsonElement element, List<double> known)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                known.Add(element.GetDouble());
                break;
            case JsonValueKind.String:
                known.AddRange(Numbers().Matches(element.GetString()!).Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture)));
                break;
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    Collect(property.Value, known);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Collect(item, known);
                }

                break;
        }
    }

    /// <summary>Digits, with decimals after a point. A full stop ending a sentence isn't one: no digit follows it.</summary>
    [GeneratedRegex(@"\d+(?:\.\d+)?")]
    private static partial Regex Numbers();
}
