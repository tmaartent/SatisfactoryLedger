namespace SatisfactoryLedger.Catalog.Domain;

public class Building
{
    private Building(BuildingId id, string name, int somersloopSlots) {
        Id = id;
        SomersloopSlots = somersloopSlots;
        Name = name;
    }

    public static Building Create(BuildingId id, string name, int somersloopSlots)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(somersloopSlots, 4);
        ArgumentOutOfRangeException.ThrowIfNegative(somersloopSlots);
        
        return new Building(id, name, somersloopSlots);
    }
    
    private bool Equals(Building other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is not null && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Building)obj));
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public BuildingId Id { get; }

    public string Name { get; }

    public int SomersloopSlots { get; }
}