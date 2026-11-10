using SatisfactoryLedger.Catalog.Domain.Import;

namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

public class Item : ICatalogEntry<Item>
{
    private Item(ItemId id, string name, ItemForm itemForm, bool isRawResource, int stackSize)
    {
        Id = id;
        Name = name;
        ItemForm = itemForm;
        IsRawResource = isRawResource;
        StackSize = stackSize;
    }

    /// <param name="stackSize">How many fit in one inventory slot (BR-A7); 0 for a fluid, which doesn't go in slots.</param>
    public static Item Create(ItemId id, string name, ItemForm itemForm, bool isRawResource, int stackSize = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfNegative(stackSize);
        return new Item(id, name, itemForm, isRawResource, stackSize);
    }
    
    private bool Equals(Item other)
    {
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj)
    {
        return obj is not null && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Item)obj));
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public ItemId Id { get; }

    public string Name { get; private set; }

    public ItemForm ItemForm { get; private set; }
    
    public bool IsRawResource { get; private set; }

    /// <summary>How many fit in one inventory slot (BR-A7); 0 for a fluid.</summary>
    public int StackSize { get; private set; }

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(Item fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is a different item than {this}", nameof(fromGame));
        }

        Name = fromGame.Name;
        ItemForm = fromGame.ItemForm;
        IsRawResource = fromGame.IsRawResource;
        StackSize = fromGame.StackSize;
        IsInGame = true;
    }

    public void MarkNoLongerInGame()
    {
        IsInGame = false;
    }

    public override string ToString() => Id.Id;
}