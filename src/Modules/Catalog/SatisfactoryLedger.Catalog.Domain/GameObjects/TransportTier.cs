using SatisfactoryLedger.Catalog.Domain.Import;

namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

public class TransportTier : ICatalogEntry<TransportTier>
{
    private static readonly int[] BeltCapacities = [60, 120, 270, 480, 780, 1200];
    private static readonly int[] PipeCapacities = [300, 600];
    private TransportTier(TransportTierId id, string name, int capacity, TransportForm form)
    {
        Id = id;
        Name = name;
        Form = form;
        Capacity = capacity;
    }

    public static TransportTier Create(TransportTierId id, string name, int capacity, TransportForm form)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        
        switch (form)
        {
            case TransportForm.Belt:
                {
                    if (!BeltCapacities.Contains(capacity))
                    {
                        throw new ArgumentException($"{capacity} is not a supported belt capacity");
                    }
                    break;
                }
            case TransportForm.Pipe:
                {
                    if (!PipeCapacities.Contains(capacity))
                    {
                        throw new ArgumentException($"{capacity} is not a supported pipe capacity");
                    }
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(form), form, null);
        }
        
        return new TransportTier(id, name, capacity, form);
    }

    public TransportForm Form { get; private set; }

    public string Name { get; private set; }

    public TransportTierId Id { get; }
    
    public int Capacity { get; private set; }

    private bool Equals(TransportTier other)
    {
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj)
    {
        return obj is not null && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((TransportTier)obj));
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(TransportTier fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is a different transporttier than {this}", nameof(fromGame));
        }

        Name = fromGame.Name;
        Form = fromGame.Form;
        Capacity = fromGame.Capacity;
        IsInGame = true;
    }

    public void MarkNoLongerInGame()
    {
        IsInGame = false;
    }

    public override string ToString() => Id.Id;
}