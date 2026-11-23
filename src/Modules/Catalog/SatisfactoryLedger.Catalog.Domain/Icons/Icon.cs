namespace SatisfactoryLedger.Catalog.Domain.Icons;

/// <summary>
/// The game's icon for one descriptor (an item, a resource, a building's), as the game shows it in its menus.
/// </summary>
/// <param name="ClassName">The descriptor's class name, as the Docs file names it: Desc_IronPlate_C, Desc_ConstructorMk1_C.</param>
/// <param name="Image">The image's bytes, as <paramref name="ContentType"/> says.</param>
public sealed record Icon(string ClassName, byte[] Image, string ContentType);
