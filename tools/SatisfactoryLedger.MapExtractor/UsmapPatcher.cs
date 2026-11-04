using System.Buffers.Binary;

namespace SatisfactoryLedger.MapExtractor;

/// <summary>
/// The game's type map (<c>CommunityResources/FactoryGame.usmap</c>) writes optional properties without the inner type
/// that CUE4Parse expects, and CUE4Parse fails on it (spike log 06). This rewrites each such property as a string.
/// None of them belong to resource nodes, so nothing the tool reads changes.
/// </summary>
/// <remarks>
/// Only the format the game ships is supported: usmap version 1 or lower, uncompressed. Anything else is refused,
/// so the tool fails clearly instead of producing a partial table (ADR-0015).
/// </remarks>
internal static class UsmapPatcher
{
    private const ushort Magic = 0x30C4;
    private const byte LatestSupportedVersion = 1;

    private const byte OptionalProperty = 28;
    private const byte StrProperty = 10;
    private const byte StructProperty = 9;
    private const byte ArrayProperty = 8;
    private const byte MapProperty = 24;
    private const byte SetProperty = 25;
    private const byte EnumProperty = 26;
    private const byte LastKnownProperty = 30; // AnsiStrProperty, the last type UE 5.6 writes

    public sealed record Result(byte[] Usmap, int OptionalsRewritten);

    public static Result Patch(byte[] usmap)
    {
        byte[] patched = [.. usmap];
        var reader = new Reader(patched);

        if (reader.UInt16() != Magic)
        {
            throw new InvalidDataException("This is not a type map (.usmap): the magic number is wrong");
        }

        byte version = reader.Byte();
        if (version > LatestSupportedVersion)
        {
            throw new NotSupportedException($"Type map version {version} is not supported, only up to {LatestSupportedVersion}");
        }

        if (version >= 1 && reader.Int32() != 0)
        {
            reader.Skip(8); // UE4 and UE5 package file versions
            reader.Skip(reader.Int32() * 20); // custom versions: a 16-byte GUID and a version each
            reader.Skip(4); // net changelist
        }

        byte compression = reader.Byte();
        if (compression != 0)
        {
            throw new NotSupportedException($"A compressed type map (method {compression}) is not supported");
        }

        reader.Skip(8); // compressed and decompressed size: the same when uncompressed

        uint names = reader.UInt32();
        for (uint i = 0; i < names; i++)
        {
            reader.Skip(reader.Byte());
        }

        uint enums = reader.UInt32();
        for (uint i = 0; i < enums; i++)
        {
            reader.Skip(4);
            reader.Skip(reader.Byte() * 4);
        }

        int rewritten = 0;
        uint structs = reader.UInt32();
        for (uint i = 0; i < structs; i++)
        {
            reader.Skip(8); // name and super struct
            reader.Skip(2); // property count
            ushort serialized = reader.UInt16();
            for (int p = 0; p < serialized; p++)
            {
                reader.Skip(2 + 1 + 4); // index, array dimension, name
                rewritten += PatchType(reader, patched);
            }
        }

        return new Result(patched, rewritten);
    }

    private static int PatchType(Reader reader, byte[] patched)
    {
        int at = reader.Position;
        byte type = reader.Byte();
        switch (type)
        {
            case OptionalProperty:
                patched[at] = StrProperty;
                return 1;
            case EnumProperty:
                int inner = PatchType(reader, patched);
                reader.Skip(4); // enum name
                return inner;
            case StructProperty:
                reader.Skip(4); // struct name
                return 0;
            case ArrayProperty or SetProperty:
                return PatchType(reader, patched);
            case MapProperty:
                return PatchType(reader, patched) + PatchType(reader, patched);
            case > LastKnownProperty:
                throw new InvalidDataException($"Unknown property type {type} at byte {at}");
            default:
                return 0; // A type without inner types or names: bool, int, float, string, object, …
        }
    }

    /// <summary>Little-endian reads over the bytes, with a position, like a BinaryReader without a stream.</summary>
    private sealed class Reader(byte[] bytes)
    {
        public int Position { get; private set; }

        public byte Byte() => bytes[Position++];

        public ushort UInt16() => Read(2, span => BinaryPrimitives.ReadUInt16LittleEndian(span));

        public int Int32() => Read(4, span => BinaryPrimitives.ReadInt32LittleEndian(span));

        public uint UInt32() => Read(4, span => BinaryPrimitives.ReadUInt32LittleEndian(span));

        public void Skip(int count)
        {
            if (count < 0 || Position + count > bytes.Length)
            {
                throw new InvalidDataException("The type map ends too early");
            }

            Position += count;
        }

        private T Read<T>(int size, ReadSpan<T> read)
        {
            if (Position + size > bytes.Length)
            {
                throw new InvalidDataException("The type map ends too early");
            }

            T value = read(bytes.AsSpan(Position, size));
            Position += size;
            return value;
        }
    }

    private delegate T ReadSpan<out T>(ReadOnlySpan<byte> span);
}
