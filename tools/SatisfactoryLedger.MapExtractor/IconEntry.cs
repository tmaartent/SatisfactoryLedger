namespace SatisfactoryLedger.MapExtractor;

/// <summary>One icon of the file the Catalog imports: its descriptor and its image.</summary>
internal sealed record IconEntry(string ClassName, string ContentType, byte[] Image);
