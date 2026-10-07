namespace LocalizationWorkbench.Core;

public enum LocresVersion : byte
{
    Legacy = 0,
    Compact = 1,
    Optimized = 2,
    OptimizedCityHash64Utf16 = 3,
}

public sealed class LocresDocument
{
    public LocresVersion Version { get; set; }
    public List<LocresNamespace> Namespaces { get; set; } = new();

    [JsonIgnore]
    public int EntryCount => Namespaces.Sum(x => x.Entries.Count);
}

public sealed class LocresNamespace
{
    public string Name { get; set; } = string.Empty;
    public List<LocresEntry> Entries { get; set; } = new();
}

public sealed class LocresEntry
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public static class LocresCodec
{
    private static readonly byte[] Magic =
    {
        0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC,
        0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B,
    };

    public static LocresDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var document = new LocresDocument();
        var first = reader.ReadBytes(Magic.Length);
        if (first.Length == Magic.Length && first.AsSpan().SequenceEqual(Magic))
        {
            var versionByte = reader.ReadByte();
            if (versionByte > (byte)LocresVersion.OptimizedCityHash64Utf16)
                throw new InvalidDataException($"Unsupported LOCRES version {versionByte}: {path}");
            document.Version = (LocresVersion)versionByte;
        }
        else
        {
            document.Version = LocresVersion.Legacy;
            stream.Position = 0;
        }

        string[]? stringTable = null;
        if (document.Version >= LocresVersion.Compact)
        {
            EnsureRemaining(stream, 8, "LOCRES string-table offset");
            var stringTableOffset = reader.ReadInt64();
            if (stringTableOffset < 0 || stringTableOffset > stream.Length - 4)
                throw new InvalidDataException($"Invalid LOCRES string-table offset {stringTableOffset}: {path}");

            var resume = stream.Position;
            stream.Position = stringTableOffset;
            var stringCount = ReadReasonableCount(reader, stream, "localized string count", 5_000_000);
            stringTable = new string[stringCount];

            for (var i = 0; i < stringCount; i++)
            {
                stringTable[i] = ReadUnrealString(reader, stream);
                if (document.Version >= LocresVersion.Optimized)
                {
                    EnsureRemaining(stream, 4, "LOCRES string refcount");
                    _ = reader.ReadInt32();
                }
            }

            stream.Position = resume;
        }

        if (document.Version >= LocresVersion.Optimized)
        {
            EnsureRemaining(stream, 4, "LOCRES entry count");
            _ = reader.ReadInt32();
        }

        var namespaceCount = ReadReasonableCount(reader, stream, "namespace count", 1_000_000);
        for (var i = 0; i < namespaceCount; i++)
        {
            if (document.Version >= LocresVersion.Optimized)
            {
                EnsureRemaining(stream, 4, "LOCRES namespace hash");
                _ = reader.ReadUInt32();
            }

            var ns = new LocresNamespace
            {
                Name = ReadUnrealString(reader, stream),
            };

            var keyCount = ReadReasonableCount(reader, stream, "namespace key count", 5_000_000);
            for (var j = 0; j < keyCount; j++)
            {
                if (document.Version >= LocresVersion.Optimized)
                {
                    EnsureRemaining(stream, 4, "LOCRES key hash");
                    _ = reader.ReadUInt32();
                }

                var key = ReadUnrealString(reader, stream);
                EnsureRemaining(stream, 4, "LOCRES source-string hash");
                _ = reader.ReadUInt32();

                string value;
                if (document.Version >= LocresVersion.Compact)
                {
                    EnsureRemaining(stream, 4, "LOCRES string-table index");
                    var stringIndex = reader.ReadInt32();
                    if (stringTable is null || stringIndex < 0 || stringIndex >= stringTable.Length)
                        throw new InvalidDataException($"LOCRES string-table index {stringIndex} is out of range: {path}");
                    value = stringTable[stringIndex];
                }
                else
                {
                    value = ReadUnrealString(reader, stream);
                }

                ns.Entries.Add(new LocresEntry
                {
                    Key = key,
                    Value = value,
                });
            }

            document.Namespaces.Add(ns);
        }

        return document;
    }

    private static int ReadReasonableCount(BinaryReader reader, Stream stream, string label, int maximum)
    {
        EnsureRemaining(stream, 4, "LOCRES " + label);
        var count = reader.ReadInt32();
        if (count < 0 || count > maximum)
            throw new InvalidDataException($"Unreasonable LOCRES {label}: {count}");
        return count;
    }

    private static string ReadUnrealString(BinaryReader reader, Stream stream)
    {
        EnsureRemaining(stream, 4, "FString length");
        var length = reader.ReadInt32();
        if (length == 0)
            return string.Empty;

        if (length < 0)
        {
            var charCount = checked(-length);
            var byteCount = checked(charCount * 2);
            EnsureRemaining(stream, byteCount, "UTF-16 FString");
            var bytes = reader.ReadBytes(byteCount);
            if (bytes.Length != byteCount || byteCount < 2 || bytes[^2] != 0 || bytes[^1] != 0)
                throw new InvalidDataException("Invalid UTF-16 FString terminator.");
            return Encoding.Unicode.GetString(bytes, 0, byteCount - 2);
        }

        EnsureRemaining(stream, length, "ANSI FString");
        var raw = reader.ReadBytes(length);
        if (raw.Length != length || length < 1 || raw[^1] != 0)
            throw new InvalidDataException("Invalid ANSI FString terminator.");

        var content = raw.AsSpan(0, raw.Length - 1);
        try
        {
            return new UTF8Encoding(false, true).GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(content);
        }
    }

    private static void EnsureRemaining(Stream stream, long required, string label)
    {
        if (required < 0 || stream.Position > stream.Length - required)
            throw new EndOfStreamException($"Unexpected end of file while reading {label}.");
    }
}
