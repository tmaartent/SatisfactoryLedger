using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Persistence.Configurations;

internal sealed class ExtractorConfiguration : IEntityTypeConfiguration<Extractor>
{
    public void Configure(EntityTypeBuilder<Extractor> extractor)
    {
        extractor.HasKey(x => x.Id);
        extractor.Property(x => x.Id).HasConversion(id => id.Id, value => ExtractorId.Of(value));
        extractor.Property(x => x.Name);
        extractor.Property(x => x.Form).HasConversion<string>();
        extractor.Property(x => x.AmountPerCycle);
        extractor.Property(x => x.CycleSeconds);
        extractor.Property(x => x.IsInGame);
        extractor.Ignore(x => x.ExtractionRate);
    }
}