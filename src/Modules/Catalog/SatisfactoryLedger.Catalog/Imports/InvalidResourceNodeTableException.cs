namespace SatisfactoryLedger.Catalog.Imports;

/// <summary>The uploaded file is not a node table the Catalog can import.</summary>
internal sealed class InvalidResourceNodeTableException(string message, Exception innerException) : Exception(message, innerException);