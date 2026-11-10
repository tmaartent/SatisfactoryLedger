namespace SatisfactoryLedger.World.Domain;

/// <summary>How an <see cref="Inventory"/> fills up (BR-A7).</summary>
public enum InventoryKind
{
    /// <summary>Slots, each a stack of one item: a storage container, or a station that loads.</summary>
    Slots,

    /// <summary>A volume of fluid: a fluid buffer.</summary>
    Fluid,

    /// <summary>The dimensional depot: a limit per item.</summary>
    Depot,
}
