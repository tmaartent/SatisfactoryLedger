namespace SatisfactoryLedger.Catalog.Imports.Exceptions;

/// <summary>The uploaded file is not icons the Catalog can import.</summary>
internal sealed class InvalidIconsException(string message, Exception innerException) : Exception(message, innerException);
