namespace SatisfactoryLedger.Assistant.Explaining;

/// <summary>The findings an explanation is for: machines held back by their inputs, or by their outputs (BR-A11).</summary>
internal enum HeldBack
{
    /// <summary>Short of an input: traced upstream (BR-A20).</summary>
    Starved,

    /// <summary>A product can't all leave: traced downstream (BR-A21).</summary>
    OutputBlocked,
}
