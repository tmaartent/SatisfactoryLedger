namespace SatisfactoryLedger.Catalog.Domain;

public class TransportTier
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

    public TransportForm Form { get; }

    public string Name { get; }

    public TransportTierId Id { get; }
    
    public int Capacity { get; }

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
}