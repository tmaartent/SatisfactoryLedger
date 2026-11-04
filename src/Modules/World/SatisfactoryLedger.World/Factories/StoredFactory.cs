using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// A factory definition as stored: it belongs to a world (a session name), not to a snapshot, and is traced in every
/// snapshot of that world (BR-W11). The domain's <see cref="FactoryDefinition"/> is made from it when tracing.
/// </summary>
internal sealed class StoredFactory
{
    public required Guid Id { get; init; }

    /// <summary>The session name of the world it belongs to.</summary>
    public required string World { get; init; }

    public required string Name { get; init; }

    /// <summary>The output belt's buildable id (BR-W2).</summary>
    public required string OutputBelt { get; init; }

    /// <summary>The input items' class names (BR-W2).</summary>
    public required List<string> InputItems { get; init; }

    public FactoryDefinition ToDefinition() =>
        new(Name, BuildableId.Of(OutputBelt), InputItems.Select(ItemId.Of));
}

internal sealed class StoredFactoryConfiguration : IEntityTypeConfiguration<StoredFactory>
{
    public void Configure(EntityTypeBuilder<StoredFactory> factory)
    {
        factory.ToTable("factories");
        factory.HasKey(f => f.Id);
        factory.Property(f => f.World);
        factory.Property(f => f.Name);
        factory.Property(f => f.OutputBelt);
        factory.PrimitiveCollection(f => f.InputItems);

        // BR-W11: within a world, factory names are unique.
        factory.HasIndex(f => new { f.World, f.Name }).IsUnique();
    }
}
