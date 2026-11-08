using System.Globalization;
using System.Text.RegularExpressions;

using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.GameData.Translator;

/// <summary>
/// Reads the game's <c>mClearanceData</c> text: a list of boxes, each with a Min and Max corner, an optional offset
/// (<c>RelativeTransform</c>: Translation and Rotation), and <c>Type=CT_Soft</c> for room kept free around the building.
/// Seen from above, so z is left out.
/// </summary>
internal static partial class ClearanceDataParser
{
    public static List<ClearanceBox> Parse(string clearanceData)
    {
        List<ClearanceBox> boxes = [];
        foreach (string box in TopLevelParts(clearanceData))
        {
            if (!box.Contains("IsValid=True", StringComparison.Ordinal))
            {
                continue;
            }

            Match min = MinCorner().Match(box);
            Match max = MaxCorner().Match(box);
            Match translation = TranslationOf().Match(box);
            Match rotation = RotationOf().Match(box);
            if (!min.Success || !max.Success)
            {
                throw new FormatException($"A clearance box without a Min or Max corner: {box}");
            }

            (double dx, double dy) = translation.Success ? (Number(translation, "x"), Number(translation, "y")) : (0, 0);
            double yaw = rotation.Success ? Yaw(Number(rotation, "x"), Number(rotation, "y"), Number(rotation, "z"), Number(rotation, "w")) : 0;

            // The four corners, turned and moved, and the box around them.
            (double X, double Y)[] corners =
            [
                .. new[] { (Number(min, "x"), Number(min, "y")), (Number(max, "x"), Number(min, "y")), (Number(max, "x"), Number(max, "y")), (Number(min, "x"), Number(max, "y")) }
                    .Select(corner => (X: (corner.Item1 * Math.Cos(yaw)) - (corner.Item2 * Math.Sin(yaw)) + dx, Y: (corner.Item1 * Math.Sin(yaw)) + (corner.Item2 * Math.Cos(yaw)) + dy))
            ];

            boxes.Add(new ClearanceBox(
                Centimetres(corners.Min(corner => corner.X)), Centimetres(corners.Min(corner => corner.Y)),
                Centimetres(corners.Max(corner => corner.X)), Centimetres(corners.Max(corner => corner.Y)),
                Soft: box.Contains("Type=CT_Soft", StringComparison.Ordinal)));
        }

        return boxes;
    }

    /// <summary>"((a),(b))" → "(a)", "(b)": the boxes, split on the commas between them.</summary>
    private static IEnumerable<string> TopLevelParts(string text)
    {
        string inner = text.Trim();
        if (inner.StartsWith('(') && inner.EndsWith(')'))
        {
            inner = inner[1..^1];
        }

        int depth = 0, start = 0;
        for (int i = 0; i < inner.Length; i++)
        {
            depth += inner[i] switch { '(' => 1, ')' => -1, _ => 0 };
            if (inner[i] == ',' && depth == 0)
            {
                yield return inner[start..i];
                start = i + 1;
            }
        }

        if (start < inner.Length)
        {
            yield return inner[start..];
        }
    }

    private static double Yaw(double x, double y, double z, double w) => Math.Atan2(2 * ((w * z) + (x * y)), 1 - (2 * ((y * y) + (z * z))));

    private static double Number(Match match, string group) => double.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

    // Rounded to a hundredth of a centimetre: a turn leaves 1e-14 dust on an exact size.
    private static decimal Centimetres(double value) => Math.Round((decimal)value, 2);

    private const string Xyz = @"\(X=(?<x>-?[\d.]+),Y=(?<y>-?[\d.]+),Z=(?<z>-?[\d.]+)";

    [GeneratedRegex(@"Min=" + Xyz)]
    private static partial Regex MinCorner();

    [GeneratedRegex(@"Max=" + Xyz)]
    private static partial Regex MaxCorner();

    [GeneratedRegex(@"Translation=" + Xyz)]
    private static partial Regex TranslationOf();

    [GeneratedRegex(@"Rotation=" + Xyz + @",W=(?<w>-?[\d.]+)")]
    private static partial Regex RotationOf();
}
