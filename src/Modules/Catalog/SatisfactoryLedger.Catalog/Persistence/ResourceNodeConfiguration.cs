using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Persistence;

internal sealed class ResourceNodeConfiguration : IEntityTypeConfiguration<ResourceNode>
{
    public void Configure(EntityTypeBuilder<ResourceNode> node)
    {
        node.HasKey(n => n.Id);
        node.Property(n => n.Id).HasConversion(id => id.Id, value => ResourceNodeId.Of(value));
        node.Property(n => n.Kind).HasConversion<string>();
        node.Property(n => n.Resource).HasConversion(id => id.Id, value => ItemId.Of(value));
        node.Property(n => n.Purity).HasConversion<string>();
        node.Property(n => n.IsInGame);
        // A value object: its fields become columns (position_x, position_y, position_z).
        node.ComplexProperty(n => n.Position);
        node.HasIndex(n => n.Resource);
    }
}