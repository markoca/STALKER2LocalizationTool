namespace STALKER2LocalizationTool.Core;

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
    public uint? StoredHash { get; set; }
    public List<LocresEntry> Entries { get; set; } = new();
}

public sealed class LocresEntry
{
    public string Key { get; set; } = string.Empty;
    public uint? StoredKeyHash { get; set; }
    public uint SourceStringHash { get; set; }
    public string Value { get; set; } = string.Empty;
}

public sealed class LocresPatchResult
{
    public int MatchedKeys { get; set; }
    public int ChangedKeys { get; set; }
    public int AmbiguousKeys { get; set; }
    public List<string> AmbiguousKeyNames { get; set; } = new();
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
            uint? namespaceHash = null;
            if (document.Version >= LocresVersion.Optimized)
            {
                EnsureRemaining(stream, 4, "LOCRES namespace hash");
                namespaceHash = reader.ReadUInt32();
            }

            var ns = new LocresNamespace
            {
                Name = ReadUnrealString(reader, stream),
                StoredHash = namespaceHash,
            };

            var keyCount = ReadReasonableCount(reader, stream, "namespace key count", 5_000_000);
            for (var j = 0; j < keyCount; j++)
            {
                uint? keyHash = null;
                if (document.Version >= LocresVersion.Optimized)
                {
                    EnsureRemaining(stream, 4, "LOCRES key hash");
                    keyHash = reader.ReadUInt32();
                }

                var key = ReadUnrealString(reader, stream);
                EnsureRemaining(stream, 4, "LOCRES source-string hash");
                var sourceHash = reader.ReadUInt32();

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
                    StoredKeyHash = keyHash,
                    SourceStringHash = sourceHash,
                    Value = value,
                });
            }

            document.Namespaces.Add(ns);
        }

        return document;
    }

    public static void Save(LocresDocument document, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        if (document.Version == LocresVersion.Legacy)
        {
            WriteLegacy(document, writer);
            return;
        }

        writer.Write(Magic);
        writer.Write((byte)document.Version);

        var stringTableOffsetPosition = stream.Position;
        writer.Write((long)0);

        long entryCountPosition = -1;
        if (document.Version >= LocresVersion.Optimized)
        {
            entryCountPosition = stream.Position;
            writer.Write(0);
        }

        writer.Write(document.Namespaces.Count);

        var stringTable = new List<(string Value, int RefCount)>();
        var stringIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        var totalEntries = 0;

        foreach (var ns in document.Namespaces)
        {
            if (document.Version >= LocresVersion.Optimized)
            {
                if (ns.StoredHash is null)
                    throw new InvalidDataException($"Cannot write LOCRES v{(byte)document.Version}: namespace '{ns.Name}' has no stored hash.");
                writer.Write(ns.StoredHash.Value);
            }

            WriteUnrealString(writer, ns.Name);
            writer.Write(ns.Entries.Count);

            foreach (var entry in ns.Entries)
            {
                if (document.Version >= LocresVersion.Optimized)
                {
                    if (entry.StoredKeyHash is null)
                        throw new InvalidDataException($"Cannot write LOCRES v{(byte)document.Version}: key '{ns.Name}::{entry.Key}' has no stored hash.");
                    writer.Write(entry.StoredKeyHash.Value);
                }

                WriteUnrealString(writer, entry.Key);
                writer.Write(entry.SourceStringHash);

                if (!stringIndices.TryGetValue(entry.Value, out var stringIndex))
                {
                    stringIndex = stringTable.Count;
                    stringIndices[entry.Value] = stringIndex;
                    stringTable.Add((entry.Value, 1));
                }
                else
                {
                    var item = stringTable[stringIndex];
                    stringTable[stringIndex] = (item.Value, item.RefCount + 1);
                }

                writer.Write(stringIndex);
                totalEntries++;
            }
        }

        var stringTableOffset = stream.Position;
        writer.Write(stringTable.Count);
        foreach (var item in stringTable)
        {
            WriteUnrealString(writer, item.Value);
            if (document.Version >= LocresVersion.Optimized)
                writer.Write(item.RefCount);
        }

        var end = stream.Position;
        stream.Position = stringTableOffsetPosition;
        writer.Write(stringTableOffset);

        if (entryCountPosition >= 0)
        {
            stream.Position = entryCountPosition;
            writer.Write(totalEntries);
        }

        stream.Position = end;
    }

    public static LocresDocument Clone(LocresDocument source)
    {
        return new LocresDocument
        {
            Version = source.Version,
            Namespaces = source.Namespaces.Select(ns => new LocresNamespace
            {
                Name = ns.Name,
                StoredHash = ns.StoredHash,
                Entries = ns.Entries.Select(entry => new LocresEntry
                {
                    Key = entry.Key,
                    StoredKeyHash = entry.StoredKeyHash,
                    SourceStringHash = entry.SourceStringHash,
                    Value = entry.Value,
                }).ToList(),
            }).ToList(),
        };
    }

    public static LocresDocument Merge(IEnumerable<LocresDocument> documents)
    {
        var docs = documents.ToList();
        if (docs.Count == 0)
            throw new InvalidDataException("No LOCRES documents were provided for merge.");

        var version = docs[0].Version;
        if (docs.Any(x => x.Version != version))
            throw new InvalidDataException("LOCRES sources use different binary versions and cannot be merged safely without recalculating package hashes.");

        var result = new LocresDocument { Version = version };
        var namespaces = new Dictionary<string, LocresNamespace>(StringComparer.Ordinal);

        foreach (var doc in docs)
        {
            foreach (var sourceNs in doc.Namespaces)
            {
                if (!namespaces.TryGetValue(sourceNs.Name, out var targetNs))
                {
                    targetNs = new LocresNamespace
                    {
                        Name = sourceNs.Name,
                        StoredHash = sourceNs.StoredHash,
                    };
                    namespaces[sourceNs.Name] = targetNs;
                    result.Namespaces.Add(targetNs);
                }
                else if (sourceNs.StoredHash is not null)
                {
                    if (targetNs.StoredHash is not null && targetNs.StoredHash != sourceNs.StoredHash)
                        throw new InvalidDataException($"LOCRES namespace hash mismatch while merging '{sourceNs.Name}'.");
                    targetNs.StoredHash = sourceNs.StoredHash;
                }

                var byKey = targetNs.Entries.ToDictionary(x => x.Key, StringComparer.Ordinal);
                foreach (var sourceEntry in sourceNs.Entries)
                {
                    if (byKey.TryGetValue(sourceEntry.Key, out var existing))
                    {
                        if (existing.StoredKeyHash is not null
                            && sourceEntry.StoredKeyHash is not null
                            && existing.StoredKeyHash != sourceEntry.StoredKeyHash)
                        {
                            throw new InvalidDataException(
                                $"LOCRES key hash mismatch while merging '{sourceNs.Name}::{sourceEntry.Key}'."
                            );
                        }

                        existing.StoredKeyHash = sourceEntry.StoredKeyHash ?? existing.StoredKeyHash;
                        existing.SourceStringHash = sourceEntry.SourceStringHash;
                        existing.Value = sourceEntry.Value;
                    }
                    else
                    {
                        var added = new LocresEntry
                        {
                            Key = sourceEntry.Key,
                            StoredKeyHash = sourceEntry.StoredKeyHash,
                            SourceStringHash = sourceEntry.SourceStringHash,
                            Value = sourceEntry.Value,
                        };
                        targetNs.Entries.Add(added);
                        byKey[sourceEntry.Key] = added;
                    }
                }
            }
        }

        return result;
    }

    public static LocresPatchResult ApplyTranslations(
        LocresDocument document,
        IReadOnlyDictionary<string, string> translations)
    {
        var result = new LocresPatchResult();
        var byPlainKey = document.Namespaces
            .SelectMany(ns => ns.Entries.Select(entry => (Namespace: ns.Name, Entry: entry)))
            .GroupBy(x => x.Entry.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var pair in translations)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                continue;

            var delimiter = pair.Key.IndexOf("::", StringComparison.Ordinal);
            if (delimiter >= 0)
            {
                var namespaceName = pair.Key[..delimiter];
                var key = pair.Key[(delimiter + 2)..];
                var exact = document.Namespaces
                    .FirstOrDefault(x => string.Equals(x.Name, namespaceName, StringComparison.Ordinal))?
                    .Entries.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
                if (exact is null)
                    continue;

                result.MatchedKeys++;
                if (!string.Equals(exact.Value, pair.Value, StringComparison.Ordinal))
                {
                    exact.Value = pair.Value;
                    result.ChangedKeys++;
                }
                continue;
            }

            if (!byPlainKey.TryGetValue(pair.Key, out var matches) || matches.Count == 0)
                continue;

            if (matches.Count > 1)
            {
                result.AmbiguousKeys++;
                result.AmbiguousKeyNames.Add(pair.Key);
                continue;
            }

            result.MatchedKeys++;
            var entry = matches[0].Entry;
            if (!string.Equals(entry.Value, pair.Value, StringComparison.Ordinal))
            {
                entry.Value = pair.Value;
                result.ChangedKeys++;
            }
        }

        return result;
    }

    public static SortedDictionary<string, string> CreateTranslationTemplate(LocresDocument document)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var grouped = document.Namespaces
            .SelectMany(ns => ns.Entries.Select(entry => (Namespace: ns.Name, Entry: entry)))
            .GroupBy(x => x.Entry.Key, StringComparer.Ordinal);

        foreach (var group in grouped)
        {
            var list = group.ToList();
            if (list.Count == 1)
            {
                result[group.Key] = list[0].Entry.Value;
                continue;
            }

            foreach (var item in list)
                result[$"{item.Namespace}::{item.Entry.Key}"] = item.Entry.Value;
        }

        return result;
    }

    public static object CreateHumanReadableDump(LocresDocument document)
    {
        return new
        {
            version = (int)document.Version,
            versionName = document.Version.ToString(),
            entryCount = document.EntryCount,
            namespaces = document.Namespaces.Select(ns => new
            {
                name = ns.Name,
                storedHash = ns.StoredHash,
                entries = ns.Entries.Select(entry => new
                {
                    key = entry.Key,
                    storedKeyHash = entry.StoredKeyHash,
                    sourceStringHash = entry.SourceStringHash,
                    value = entry.Value,
                }).ToList(),
            }).ToList(),
        };
    }

    public static void VerifyEquivalent(LocresDocument expected, LocresDocument actual, string label)
    {
        if (expected.Version != actual.Version)
            throw new InvalidDataException($"{label}: LOCRES version changed from {(int)expected.Version} to {(int)actual.Version}.");

        var expectedMap = Flatten(expected);
        var actualMap = Flatten(actual);
        if (expectedMap.Count != actualMap.Count)
            throw new InvalidDataException($"{label}: LOCRES entry count changed from {expectedMap.Count} to {actualMap.Count}.");

        foreach (var pair in expectedMap)
        {
            if (!actualMap.TryGetValue(pair.Key, out var actualItem))
                throw new InvalidDataException($"{label}: LOCRES entry disappeared: {pair.Key.Replace('\u0000', ':')}");

            var expectedItem = pair.Value;
            if (expectedItem.NamespaceHash != actualItem.NamespaceHash)
                throw new InvalidDataException($"{label}: namespace hash changed: {pair.Key.Replace('\u0000', ':')}");
            if (expectedItem.Entry.StoredKeyHash != actualItem.Entry.StoredKeyHash)
                throw new InvalidDataException($"{label}: key hash changed: {pair.Key.Replace('\u0000', ':')}");
            if (expectedItem.Entry.SourceStringHash != actualItem.Entry.SourceStringHash)
                throw new InvalidDataException($"{label}: source-string hash changed: {pair.Key.Replace('\u0000', ':')}");
            if (!string.Equals(expectedItem.Entry.Value, actualItem.Entry.Value, StringComparison.Ordinal))
                throw new InvalidDataException($"{label}: localized value changed during LOCRES round-trip: {pair.Key.Replace('\u0000', ':')}");
        }
    }

    private sealed record FlattenedLocresEntry(uint? NamespaceHash, LocresEntry Entry);

    private static Dictionary<string, FlattenedLocresEntry> Flatten(LocresDocument document)
    {
        var result = new Dictionary<string, FlattenedLocresEntry>(StringComparer.Ordinal);
        foreach (var ns in document.Namespaces)
        {
            foreach (var entry in ns.Entries)
            {
                var composite = ns.Name + "\u0000" + entry.Key;
                if (!result.TryAdd(composite, new FlattenedLocresEntry(ns.StoredHash, entry)))
                    throw new InvalidDataException($"Duplicate LOCRES namespace/key pair: {ns.Name}::{entry.Key}");
            }
        }
        return result;
    }

    private static void WriteLegacy(LocresDocument document, BinaryWriter writer)
    {
        writer.Write(document.Namespaces.Count);
        foreach (var ns in document.Namespaces)
        {
            WriteUnrealString(writer, ns.Name, forceUnicode: true);
            writer.Write(ns.Entries.Count);
            foreach (var entry in ns.Entries)
            {
                WriteUnrealString(writer, entry.Key);
                writer.Write(entry.SourceStringHash);
                WriteUnrealString(writer, entry.Value);
            }
        }
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

    private static void WriteUnrealString(BinaryWriter writer, string value, bool forceUnicode = false)
    {
        value ??= string.Empty;
        if (value.Length == 0)
        {
            writer.Write(0);
            return;
        }

        var ascii = !forceUnicode && value.All(ch => ch <= 0x7F);
        if (ascii)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length + 1);
            writer.Write(bytes);
            writer.Write((byte)0);
            return;
        }

        var utf16 = Encoding.Unicode.GetBytes(value);
        var codeUnitsWithNull = checked(utf16.Length / 2 + 1);
        writer.Write(-codeUnitsWithNull);
        writer.Write(utf16);
        writer.Write((ushort)0);
    }

    private static void EnsureRemaining(Stream stream, long required, string label)
    {
        if (required < 0 || stream.Position > stream.Length - required)
            throw new EndOfStreamException($"Unexpected end of file while reading {label}.");
    }
}
