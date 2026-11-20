namespace SatisfactoryLedger.Catalog.Imports.Exceptions;

/// <summary>The uploaded file is not map art the Catalog can import.</summary>
internal sealed class InvalidMapArtException(string message, Exception innerException) : Exception(message, innerException);
