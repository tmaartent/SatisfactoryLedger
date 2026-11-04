namespace SatisfactoryLedger.World.SaveFile;

/// <summary>The save can't be read, or what it contains doesn't add up. An import fails with this, never with half a world.</summary>
public sealed class SaveFileException : Exception
{
    public SaveFileException(string message) : base(message)
    {
    }

    public SaveFileException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
