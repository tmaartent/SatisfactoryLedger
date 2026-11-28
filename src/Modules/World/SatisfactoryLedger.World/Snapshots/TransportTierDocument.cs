using System.Text.Json.Serialization;

using SatisfactoryLedger.World.SaveFile;

namespace SatisfactoryLedger.World.Snapshots;

/// <param name="Id">The building's class name, e.g. <c>Build_ConveyorBeltMk1_C</c>.</param>
internal sealed record TransportTierDocument(string Id, int CapacityPerMinute)
{
    /// <summary>Belt or lift, or pipe, as the save-file adapter decides it from the class: derived on read, not stored.</summary>
    [JsonIgnore]
    public BuildableKind BuildableKind => GameClasses.KindOf(Id);
}