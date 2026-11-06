using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;

using SatisfactorySaveNet;
using SatisfactorySaveNet.Abstracts.Model;
using SatisfactorySaveNet.Abstracts.Model.Properties;
using SatisfactorySaveNet.Abstracts.Model.Typed;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>
/// Reads a save file into a <see cref="SavedWorld"/> (ADR-0013). This is the only code that uses the SatisfactorySaveNet
/// library: its types never leave this class, and its errors leave as a <see cref="SaveFileException"/>.
/// </summary>
public static class SaveFileReader
{
    private static readonly HashSet<string> ConnectorClasses =
        ["FGFactoryConnectionComponent", "FGPipeConnectionComponent", "FGPipeConnectionFactory"];

    public static SavedWorld Read(Stream save)
    {
        BodyV8 body;
        SatisfactorySave parsed;
        try
        {
            parsed = SaveFileSerializer.Instance.Deserialize(save);
            body = parsed.Body as BodyV8 ?? throw new SaveFileException("Only saves of game version 1.0 and later can be read");
        }
        // The library throws its own exceptions, but also plain ones (EndOfStreamException, NotSupportedException).
        // At this boundary, any failure to parse means the same thing: this file can't be read as a save.
        catch (Exception e) when (e is not (SaveFileException or OutOfMemoryException))
        {
            throw new SaveFileException($"The file can't be read as a Satisfactory save: {e.Message}", e);
        }

        try
        {
            return Translate(parsed.Header, body);
        }
        catch (ArgumentException e)
        {
            throw new SaveFileException($"The save contains something the adapter can't translate: {e.Message}", e);
        }
    }

    private static SavedWorld Translate(Header header, BodyV8 body)
    {
        Dictionary<string, ComponentObject> byPath = [];
        foreach (ComponentObject saved in body.Levels.SelectMany(level => level.Objects))
        {
            byPath.TryAdd(saved.ObjectReference.PathName, saved);
        }

        List<SavedBuildable> buildables = [];
        List<Connector> connectors = [];
        Dictionary<string, int> unsupported = [];

        foreach (ActorObject actor in byPath.Values.OfType<ActorObject>())
        {
            string className = ShortName(actor.TypePath);
            if (!className.StartsWith("Build_", StringComparison.Ordinal))
            {
                continue;
            }

            var id = BuildableId.Of(actor.ObjectReference.PathName);
            List<(string Path, string Port, string? LinkedTo)> ports = PortsOf(actor, byPath);
            BuildableRole role = GameClasses.RoleOf(className);
            bool linked = ports.Any(port => port.LinkedTo is not null);
            bool unloads = ports.Any(port => port.LinkedTo is not null && GameClasses.DirectionOf(port.Port, className) == Port.Out);
            string? inventory = GameClasses.StockInventoryOf(className);
            IReadOnlySet<string>? burns = GameClasses.FuelInventoryOf(className) is { } fuelInventory
                ? StockOf(actor, fuelInventory, byPath).Keys.ToHashSet(StringComparer.Ordinal)
                : null;

            SavedBuildable? buildable = role switch
            {
                BuildableRole.Machine => new SavedMachine(id, className, ShortNameOrNull(ObjectPath(actor, "mCurrentRecipe")),
                    ClockSpeedOf(actor), (decimal?)Float(actor, "mCurrentProductionBoost") ?? 1m),
                BuildableRole.Provider => new SavedProvider(id, className, ShortNameOrNull(ObjectPath(actor, "mExtractableResource")),
                    ClockSpeedOf(actor), GameClasses.FixedResourceOf(className)),
                BuildableRole.Consumer => new SavedConsumer(id, className),
                // BR-W1, BR-W8: belts leaving a station's outputs make it unload; belts into its inputs only, load.
                BuildableRole.Station when unloads => new SavedProvider(id, className, Node: null, ClockSpeed.Percent(100))
                {
                    Stock = StockOf(actor, inventory!, byPath),
                    Burns = burns,
                },
                BuildableRole.Station when linked => new SavedConsumer(id, className) { Burns = burns },
                BuildableRole.Logistics => new SavedLogistics(id, className, GameClasses.LogisticsKindOf(className))
                {
                    Stock = inventory is null ? null : StockOf(actor, inventory, byPath),
                },
                _ => null,
            };

            if (buildable is not null)
            {
                buildables.Add(buildable with { Position = new SavedPosition(actor.Position.X, actor.Position.Y, actor.Position.Z) });
                connectors.AddRange(ports.Select(port =>
                    new Connector(id, port.Path, GameClasses.DirectionOf(port.Port, className), port.LinkedTo)));
            }
            else if (role is BuildableRole.Unsupported || linked)
            {
                // Not in the world, but its ports stay, so the links pointing at it can still be checked.
                unsupported[className] = unsupported.GetValueOrDefault(className) + 1;
                connectors.AddRange(ports.Select(port => new Connector(id, port.Path, Port.Undirected, port.LinkedTo)));
            }
        }

        HashSet<BuildableId> inWorld = [.. buildables.Select(buildable => buildable.Id)];
        return new SavedWorld(
            new SaveInfo(header.SessionName, header.BuildVersion, header.SaveDateTimeUtc),
            buildables,
            ConnectionBuilder.Build(connectors, inWorld),
            [.. unsupported.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => new UnsupportedBuildables(entry.Key, entry.Value))]);
    }

    /// <summary>The belt and pipe ports of a buildable: <c>…Build_SmelterMk1_C_123.Input0</c>, and what each is linked to.</summary>
    private static List<(string Path, string Port, string? LinkedTo)> PortsOf(ActorObject actor, Dictionary<string, ComponentObject> byPath) =>
    [
        .. actor.Components
            .Select(reference => byPath.GetValueOrDefault(reference.PathName))
            .OfType<ComponentObject>()
            .Where(component => ConnectorClasses.Contains(ShortName(component.TypePath)))
            .Select(component => (
                Path: component.ObjectReference.PathName,
                Port: ShortName(component.ObjectReference.PathName),
                LinkedTo: ObjectPath(component, "mConnectedComponent")))
            // Snap points hold up belts and pipes; they don't carry anything.
            .Where(port => !port.Port.StartsWith("SnapOnly", StringComparison.Ordinal))
    ];

    /// <summary>
    /// What one inventory of a buildable holds, per item: its stacks are an item path and a count each. An empty slot
    /// has no item.
    /// </summary>
    private static Dictionary<string, decimal> StockOf(ActorObject actor, string inventory, Dictionary<string, ComponentObject> byPath)
    {
        Dictionary<string, decimal> stock = new(StringComparer.Ordinal);
        ComponentObject? component = byPath.GetValueOrDefault($"{actor.ObjectReference.PathName}.{inventory}");
        var stacks = component?.Properties.OfType<ArrayProperty>().FirstOrDefault(property => property.Name == "mInventoryStacks")?.Property
            as ArrayStructProperty;

        foreach (ArrayProperties stack in stacks?.Values.OfType<ArrayProperties>() ?? [])
        {
            if (stack.Values.OfType<StructProperty>().FirstOrDefault(property => property.Name == "Item")?.Value is InventoryItem { ItemType.Length: > 0 } item
                && item.ExtraProperty is IntProperty { Value: > 0 } count)
            {
                string itemId = ShortName(item.ItemType);
                stock[itemId] = stock.GetValueOrDefault(itemId) + count.Value;
            }
        }

        return stock;
    }

    /// <summary>
    /// The save leaves out values that equal the default, so a missing clock speed is 100%. The save stores a float
    /// (0.333333); the game works to 4 decimals of a percent (BR-B9), so round there before making it exact.
    /// </summary>
    private static ClockSpeed ClockSpeedOf(ComponentObject buildable)
    {
        float? potential = Float(buildable, "mCurrentPotential");
        return potential is null ? ClockSpeed.Percent(100) : ClockSpeed.Percent(Math.Round((decimal)potential.Value * 100, 4));
    }

    private static float? Float(ComponentObject saved, string name) =>
        saved.Properties.OfType<FloatProperty>().FirstOrDefault(property => property.Name == name)?.Value;

    private static string? ObjectPath(ComponentObject saved, string name) =>
        saved.Properties.OfType<ObjectProperty>().FirstOrDefault(property => property.Name == name)?.Value.PathName;

    /// <summary>The last part of a path: <c>/Game/…/Recipe_IronPlate.Recipe_IronPlate_C</c> becomes <c>Recipe_IronPlate_C</c>.</summary>
    private static string ShortName(string path) => path[(path.LastIndexOf('.') + 1)..];

    private static string? ShortNameOrNull(string? path) => path is null ? null : ShortName(path);
}
