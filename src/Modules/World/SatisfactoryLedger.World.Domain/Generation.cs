using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// What a generator burning a known fuel does per minute at full power and its clock speed (BR-A8): burns its fuel,
/// needs its supplemental fluid (water), and leaves its byproduct (uranium or plutonium waste).
/// </summary>
public sealed record Generation(ItemId Fuel, Rate Burns, ItemId? Supplemental, Rate SupplementalNeeded, ItemId? Byproduct, Rate Leaves);