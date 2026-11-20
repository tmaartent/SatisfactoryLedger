using System.Globalization;
using System.Text.RegularExpressions;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.GameData.Translator;

/// <summary>
/// Reads the game's <c>mClearanceData</c> text: a list of boxes, each with a Min and Max corner, an optional offset
/// (<c>RelativeTransform</c>: Translation and Rotation), and <c>Type=CT_Soft</c> for room kept free around the building.
/// Each box twice: seen from above (its rectangle, turned and moved), and in 3D as the file has it (its volume).
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
            (double X, double Y, double Z, double W) turn = rotation.Success
                ? (Number(rotation, "x"), Number(rotation, "y"), Number(rotation, "z"), Number(rotation, "w"))
                : (0, 0, 0, 1);

            // The eight corners, turned in 3D and moved, and the box around them seen from above: a box tilted up (the
            // Particle Accelerator's arch) is shorter seen from above than its length (fixed 2026-11-18).
            (double X, double Y)[] corners =
            [
                .. from x in new[] { Number(min, "x"), Number(max, "x") }
                   from y in new[] { Number(min, "y"), Number(max, "y") }
                   from z in new[] { Number(min, "z"), Number(max, "z") }
                   let turned = Turn(turn, (x, y, z))
                   select (turned.X + dx, turned.Y + dy),
            ];

            boxes.Add(new ClearanceBox(
                Centimetres(corners.Min(corner => corner.X)), Centimetres(corners.Min(corner => corner.Y)),
                Centimetres(corners.Max(corner => corner.X)), Centimetres(corners.Max(corner => corner.Y)),
                Soft: box.Contains("Type=CT_Soft", StringComparison.Ordinal))
            {
                Volume = new ClearanceVolume(
                    Exact(min, "x"), Exact(min, "y"), Exact(min, "z"), Exact(max, "x"), Exact(max, "y"), Exact(max, "z"),
                    rotation.Success ? Exact(rotation, "x") : 0, rotation.Success ? Exact(rotation, "y") : 0,
                    rotation.Success ? Exact(rotation, "z") : 0, rotation.Success ? Exact(rotation, "w") : 1,
                    translation.Success ? Exact(translation, "x") : 0, translation.Success ? Exact(translation, "y") : 0,
                    translation.Success ? Exact(translation, "z") : 0),
            });
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

    /// <summary>Turns a point by a quaternion: v + 2w(q × v) + 2q × (q × v).</summary>
    private static (double X, double Y, double Z) Turn((double X, double Y, double Z, double W) q, (double X, double Y, double Z) v)
    {
        (double X, double Y, double Z) c = ((q.Y * v.Z) - (q.Z * v.Y), (q.Z * v.X) - (q.X * v.Z), (q.X * v.Y) - (q.Y * v.X));
        (double X, double Y, double Z) cc = ((q.Y * c.Z) - (q.Z * c.Y), (q.Z * c.X) - (q.X * c.Z), (q.X * c.Y) - (q.Y * c.X));
        return (v.X + (2 * q.W * c.X) + (2 * cc.X), v.Y + (2 * q.W * c.Y) + (2 * cc.Y), v.Z + (2 * q.W * c.Z) + (2 * cc.Z));
    }

    /// <summary>A number as the file writes it ("0.965926"), without the trailing zeros a decimal would keep.</summary>
    private static decimal Exact(Match match, string group) =>
        decimal.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture) / 1.000000000000000000000000000000000m;

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
