using System.Text;

namespace SatisfactoryLedger.MapExtractor.Tests;

/// <summary>
/// The type map the game ships writes optional properties without an inner type, which CUE4Parse can't read
/// (spike log 06). The patcher rewrites them, so CUE4Parse can load the rest.
/// </summary>
public class UsmapPatcherTests
{
    private const byte Optional = 28;
    private const byte Str = 10;
    private const byte Int = 2;

    /// <summary>
    /// A minimal type map in the game's format (version 1, uncompressed): one struct with an optional property,
    /// a string and an int. Returns the bytes and where each property's type byte is.
    /// </summary>
    private static (byte[] Usmap, int[] TypeOffsets) TypeMap(byte version = 1, byte compression = 0)
    {
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data);
        string[] names = ["TestStruct", "OptionalName", "PlainString", "Count"];
        writer.Write((uint)names.Length);
        foreach (string name in names)
        {
            writer.Write((byte)name.Length);
            writer.Write(Encoding.ASCII.GetBytes(name));
        }

        writer.Write(0u); // enums
        writer.Write(1u); // structs
        writer.Write(0u); // struct name: TestStruct
        writer.Write(uint.MaxValue); // no super struct
        writer.Write((ushort)3);
        writer.Write((ushort)3);
        List<int> offsets = [];
        foreach ((ushort index, uint name, byte type) in new (ushort, uint, byte)[] { (0, 1, Optional), (1, 2, Str), (2, 3, Int) })
        {
            writer.Write(index);
            writer.Write((byte)1); // array dim
            writer.Write(name);
            offsets.Add((int)data.Position);
            writer.Write(type);
        }

        byte[] body = data.ToArray();
        using var file = new MemoryStream();
        using var header = new BinaryWriter(file);
        header.Write((ushort)0x30C4);
        header.Write(version);
        header.Write(0); // no package versioning info
        header.Write(compression);
        header.Write((uint)body.Length);
        header.Write((uint)body.Length);
        int bodyStart = (int)file.Position;
        header.Write(body);

        return (file.ToArray(), [.. offsets.Select(offset => bodyStart + offset)]);
    }

    [Fact]
    public void An_optional_property_without_an_inner_type_becomes_a_string()
    {
        (byte[] usmap, int[] types) = TypeMap();

        byte[] patched = UsmapPatcher.Patch(usmap).Usmap;

        Assert.Equal(Str, patched[types[0]]);
    }

    [Fact]
    public void Other_properties_are_left_as_they_are()
    {
        (byte[] usmap, int[] types) = TypeMap();

        byte[] patched = UsmapPatcher.Patch(usmap).Usmap;

        Assert.Equal(Str, patched[types[1]]);
        Assert.Equal(Int, patched[types[2]]);
        Assert.Equal(usmap.Length, patched.Length);
    }

    [Fact]
    public void The_patcher_says_how_many_properties_it_rewrote()
    {
        Assert.Equal(1, UsmapPatcher.Patch(TypeMap().Usmap).OptionalsRewritten);
    }

    [Fact]
    public void A_compressed_type_map_is_refused()
    {
        Assert.Throws<NotSupportedException>(() => UsmapPatcher.Patch(TypeMap(compression: 1).Usmap));
    }

    [Fact]
    public void A_type_map_in_a_newer_format_is_refused()
    {
        Assert.Throws<NotSupportedException>(() => UsmapPatcher.Patch(TypeMap(version: 2).Usmap));
    }

    [Fact]
    public void A_file_that_is_not_a_type_map_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => UsmapPatcher.Patch([1, 2, 3, 4, 5, 6]));
    }
}
