namespace SatisfactoryLedger.Catalog.Imports.Exceptions;

/// <summary>The uploaded file is not a Docs file the importer can read, or its data breaks a catalog rule.</summary>
internal sealed class InvalidDocsFileException(string message, Exception innerException) : Exception(message, innerException);