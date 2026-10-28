namespace SatisfactoryLedger.Catalog.Domain;

public class Item
{
    private Item(ItemId id, string name, ItemForm itemForm, bool isRawResource)
    {
        Id = id;
        Name = name;
        ItemForm = itemForm;
        IsRawResource = isRawResource;
    }

    public static Item Create(ItemId id, string name, ItemForm itemForm, bool isRawResource)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return new Item(id, name, itemForm, isRawResource);
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

    public string Name { get; }

    public ItemForm ItemForm { get; }
    
    public bool IsRawResource { get; }
    
    
}