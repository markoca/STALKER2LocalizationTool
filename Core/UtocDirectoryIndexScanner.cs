using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Core;

/// <summary>
/// Lightweight reader for the unencrypted Unreal IoStore directory index stored
/// directly inside a .utoc file. MODS scanning only needs virtual package paths
/// and their chunk IDs, so it can avoid opening/decompressing the companion UCAS.
/// </summary>
public static class UtocDirectoryIndexScanner
{
    private const int TocHeaderSize = 0x90;
    private const byte EncryptedFlag = 0x02;
    private const byte SignedFlag = 0x04;
    private const byte IndexedFlag = 0x08;
    private const uint InvalidIndex = uint.MaxValue;
    private const int MaxIndexEntries = 10_000_000;
    private const int MaxStringUnits = 16_777_216;

    private static readonly byte[] TocMagic =
        Encoding.ASCII.GetBytes("-==--==--==--==-");

    public static bool TryListLocalizationAssets(
        string utocPath,
        string modsRoot,
        out List<LocalizationAlias> assets,
        out string? fallbackReason)
    {
        assets = new List<LocalizationAlias>();
        fallbackReason = null;

        try
        {
            assets = ReadLocalizationAssets(
                utocPath,
                modsRoot
            );
            return true;
        }
        catch (NotSupportedException ex)
        {
            fallbackReason = ex.Message;
            return false;
        }
        catch (InvalidDataException ex)
        {
            fallbackReason = ex.Message;
            return false;
        }
        catch (EndOfStreamException ex)
        {
            fallbackReason = ex.Message;
            return false;
        }
        catch (IOException ex)
        {
            fallbackReason = ex.Message;
            return false;
        }
        catch (OverflowException ex)
        {
            fallbackReason = ex.Message;
            return false;
        }
    }

    private static List<LocalizationAlias> ReadLocalizationAssets(
        string utocPath,
        string modsRoot)
    {
        using var stream = new FileStream(
            utocPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            64 * 1024,
            FileOptions.SequentialScan
        );
        using var reader = new BinaryReader(
            stream,
            Encoding.UTF8,
            leaveOpen: true
        );

        var header = ReadExact(
            reader,
            TocHeaderSize
        );
        if (!header.AsSpan(0, TocMagic.Length)
                .SequenceEqual(TocMagic))
        {
            throw new InvalidDataException(
                "Unrecognized IoStore TOC magic."
            );
        }

        var version = header[16];
        var headerSize = ReadUInt32(header, 20);
        if (headerSize != (uint)TocHeaderSize)
        {
            throw new NotSupportedException(
                $"Unsupported IoStore TOC header size: {headerSize}."
            );
        }

        var entryCount = ReadUInt32(header, 24);
        var compressedBlockCount = ReadUInt32(header, 28);
        var compressedBlockEntrySize = ReadUInt32(header, 32);
        var compressionMethodCount = ReadUInt32(header, 36);
        var compressionMethodNameLength = ReadUInt32(header, 40);
        var directoryIndexSize = ReadUInt32(header, 48);
        var containerFlags = header[80];
        var perfectHashSeedCount = ReadUInt32(header, 84);
        var chunksWithoutPerfectHashCount = ReadUInt32(header, 96);

        if ((containerFlags & EncryptedFlag) != 0)
        {
            throw new NotSupportedException(
                "Encrypted IoStore directory index requires retoc fallback."
            );
        }

        if ((containerFlags & IndexedFlag) == 0
            || directoryIndexSize == 0)
        {
            return new List<LocalizationAlias>();
        }

        if (entryCount > MaxIndexEntries
            || compressedBlockCount > MaxIndexEntries
            || perfectHashSeedCount > MaxIndexEntries
            || chunksWithoutPerfectHashCount > MaxIndexEntries)
        {
            throw new InvalidDataException(
                "IoStore TOC count exceeds the safety limit."
            );
        }

        if (compressedBlockEntrySize == 0
            || compressedBlockEntrySize > 1024)
        {
            throw new InvalidDataException(
                $"Invalid IoStore compression-block entry size: "
                + $"{compressedBlockEntrySize}."
            );
        }

        var chunkIds = new byte[checked((int)entryCount)][];
        for (var index = 0; index < chunkIds.Length; index++)
            chunkIds[index] = ReadExact(reader, 12);

        Skip(
            stream,
            checked((long)entryCount * 10L)
        );

        // retoc only consumes the hash-map arrays for the TOC versions that
        // actually serialize them.
        if (version >= 5)
        {
            Skip(
                stream,
                checked((long)perfectHashSeedCount * 4L)
            );
            Skip(
                stream,
                checked((long)chunksWithoutPerfectHashCount * 4L)
            );
        }
        else if (version >= 4)
        {
            Skip(
                stream,
                checked((long)perfectHashSeedCount * 4L)
            );
        }

        Skip(
            stream,
            checked(
                (long)compressedBlockCount
                * compressedBlockEntrySize
            )
        );
        Skip(
            stream,
            checked(
                (long)compressionMethodCount
                * compressionMethodNameLength
            )
        );

        if ((containerFlags & SignedFlag) != 0)
        {
            var signatureSize = reader.ReadUInt32();
            Skip(
                stream,
                checked((long)signatureSize * 2L)
            );
            Skip(
                stream,
                checked((long)compressedBlockCount * 20L)
            );
        }

        if (directoryIndexSize > int.MaxValue)
        {
            throw new InvalidDataException(
                "IoStore directory index is too large."
            );
        }

        var directoryIndex = ReadExact(
            reader,
            checked((int)directoryIndexSize)
        );

        return ParseDirectoryIndex(
            directoryIndex,
            chunkIds,
            utocPath,
            modsRoot
        );
    }

    private static List<LocalizationAlias> ParseDirectoryIndex(
        byte[] buffer,
        IReadOnlyList<byte[]> chunkIds,
        string utocPath,
        string modsRoot)
    {
        using var stream = new MemoryStream(
            buffer,
            writable: false
        );
        using var reader = new BinaryReader(
            stream,
            Encoding.UTF8,
            leaveOpen: false
        );

        var mountPoint = ReadFString(reader);

        var directoryCount = ReadCount(
            reader,
            "directory"
        );
        var directories = new DirectoryEntry[directoryCount];
        for (var index = 0; index < directories.Length; index++)
        {
            directories[index] = new DirectoryEntry(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32()
            );
        }

        var fileCount = ReadCount(
            reader,
            "file"
        );
        var files = new FileEntry[fileCount];
        for (var index = 0; index < files.Length; index++)
        {
            files[index] = new FileEntry(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32()
            );
        }

        var stringCount = ReadCount(
            reader,
            "string"
        );
        var strings = new string[stringCount];
        for (var index = 0; index < strings.Length; index++)
            strings[index] = ReadFString(reader);

        if (directories.Length == 0)
            return new List<LocalizationAlias>();

        var candidateNames = strings
            .Select((value, index) => (value, index))
            .Where(item => IsLocalizationDatabaseName(item.value))
            .Select(item => (uint)item.index)
            .ToHashSet();

        if (candidateNames.Count == 0)
            return new List<LocalizationAlias>();

        var results = new List<LocalizationAlias>();
        var seenAliases = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );
        var visitedDirectories = new HashSet<uint>();
        var stack = new Stack<(uint Directory, string Parent)>();
        stack.Push((0, string.Empty));

        while (stack.Count > 0)
        {
            var (directoryIndex, parentPath) = stack.Pop();
            if (directoryIndex >= (uint)directories.Length)
            {
                throw new InvalidDataException(
                    "IoStore directory index points outside the directory table."
                );
            }

            if (!visitedDirectories.Add(directoryIndex))
            {
                throw new InvalidDataException(
                    "IoStore directory index contains a directory cycle."
                );
            }

            var directory = directories[(int)directoryIndex];
            var currentPath = parentPath;

            if (directory.Name != InvalidIndex)
            {
                var name = GetString(
                    strings,
                    directory.Name,
                    "directory"
                );
                currentPath = CombineVirtualPath(
                    parentPath,
                    name
                );
            }

            var visitedFiles = new HashSet<uint>();
            var fileIndex = directory.FirstFile;
            while (fileIndex != InvalidIndex)
            {
                if (fileIndex >= (uint)files.Length)
                {
                    throw new InvalidDataException(
                        "IoStore directory index points outside the file table."
                    );
                }
                if (!visitedFiles.Add(fileIndex))
                {
                    throw new InvalidDataException(
                        "IoStore directory index contains a file cycle."
                    );
                }

                var file = files[(int)fileIndex];
                if (candidateNames.Contains(file.Name))
                {
                    if (file.UserData >= (uint)chunkIds.Count)
                    {
                        throw new InvalidDataException(
                            "IoStore file entry points outside the chunk table."
                        );
                    }

                    var fileName = GetString(
                        strings,
                        file.Name,
                        "file"
                    );
                    var relativePath = CombineVirtualPath(
                        currentPath,
                        fileName
                    );
                    var virtualPath = CombineMountPoint(
                        mountPoint,
                        relativePath
                    );
                    var chunkId = Convert.ToHexString(
                        chunkIds[(int)file.UserData]
                    ).ToLowerInvariant();

                    var key = chunkId + "|" + virtualPath;
                    if (seenAliases.Add(key))
                    {
                        results.Add(new LocalizationAlias
                        {
                            ZenChunkId = chunkId,
                            VirtualPath = virtualPath,
                            SourceUtoc = utocPath,
                            SourceUtocRelative = Path.GetRelativePath(
                                modsRoot,
                                utocPath
                            ),
                        });
                    }
                }

                fileIndex = file.NextFile;
            }

            var childIndex = directory.FirstChild;
            var siblingGuard = new HashSet<uint>();
            while (childIndex != InvalidIndex)
            {
                if (childIndex >= (uint)directories.Length)
                {
                    throw new InvalidDataException(
                        "IoStore child directory points outside the table."
                    );
                }
                if (!siblingGuard.Add(childIndex))
                {
                    throw new InvalidDataException(
                        "IoStore directory index contains a sibling cycle."
                    );
                }

                stack.Push((
                    childIndex,
                    currentPath
                ));
                childIndex = directories[(int)childIndex].NextSibling;
            }
        }

        return results
            .OrderBy(
                item => item.VirtualPath,
                StringComparer.OrdinalIgnoreCase
            )
            .ThenBy(
                item => item.ZenChunkId,
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();
    }

    private static bool IsLocalizationDatabaseName(string fileName)
    {
        // ZoneKit-generated databases normally use an Autogenerated_* prefix.
        // Keep the old suffix-based compatibility rule as well: direct UTOC
        // indexing is already cheap, so there is no reason to drop custom/legacy
        // LocalizationDatabase package names that the previous retoc scan found.
        return fileName.EndsWith(
            AppConstants.LocalizationDatabaseNeedle,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static int ReadCount(
        BinaryReader reader,
        string label)
    {
        var count = reader.ReadUInt32();
        if (count > MaxIndexEntries)
        {
            throw new InvalidDataException(
                $"IoStore {label} table exceeds the safety limit."
            );
        }

        return checked((int)count);
    }

    private static string GetString(
        IReadOnlyList<string> strings,
        uint index,
        string label)
    {
        if (index >= (uint)strings.Count)
        {
            throw new InvalidDataException(
                $"IoStore {label} name points outside the string table."
            );
        }

        return strings[(int)index];
    }

    private static string ReadFString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length == 0)
            return string.Empty;

        if (length > 0)
        {
            if (length > MaxStringUnits)
            {
                throw new InvalidDataException(
                    "IoStore ANSI string is too large."
                );
            }

            var ansiBytes = ReadExact(
                reader,
                length
            );
            var ansiTerminator = Array.IndexOf(
                ansiBytes,
                (byte)0
            );
            var contentLength = ansiTerminator >= 0
                ? ansiTerminator
                : ansiBytes.Length;
            return Encoding.UTF8.GetString(
                ansiBytes,
                0,
                contentLength
            );
        }

        if (length == int.MinValue)
        {
            throw new InvalidDataException(
                "Invalid IoStore UTF-16 string length."
            );
        }

        var characterCount = -length;
        if (characterCount > MaxStringUnits)
        {
            throw new InvalidDataException(
                "IoStore UTF-16 string is too large."
            );
        }

        var bytesLength = checked(characterCount * 2);
        var bytes = ReadExact(
            reader,
            bytesLength
        );
        var value = Encoding.Unicode.GetString(bytes);
        var terminator = value.IndexOf('\0');
        return terminator >= 0
            ? value[..terminator]
            : value;
    }

    private static string CombineVirtualPath(
        string parent,
        string child)
    {
        if (string.IsNullOrEmpty(parent))
            return child.Trim('/');

        if (string.IsNullOrEmpty(child))
            return parent.Trim('/');

        return parent.TrimEnd('/')
               + "/"
               + child.Trim('/');
    }

    private static string CombineMountPoint(
        string mountPoint,
        string relativePath)
    {
        if (string.IsNullOrEmpty(mountPoint))
            return relativePath;

        return mountPoint.EndsWith(
                "/",
                StringComparison.Ordinal)
            ? mountPoint + relativePath
            : mountPoint + "/" + relativePath;
    }

    private static uint ReadUInt32(
        byte[] buffer,
        int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(
            buffer.AsSpan(offset, 4)
        );
    }

    private static byte[] ReadExact(
        BinaryReader reader,
        int count)
    {
        var data = reader.ReadBytes(count);
        if (data.Length != count)
            throw new EndOfStreamException();
        return data;
    }

    private static void Skip(
        Stream stream,
        long count)
    {
        if (count < 0)
            throw new InvalidDataException("Negative IoStore skip length.");

        var destination = checked(stream.Position + count);
        if (destination > stream.Length)
            throw new EndOfStreamException();

        stream.Position = destination;
    }

    private readonly record struct DirectoryEntry(
        uint Name,
        uint FirstChild,
        uint NextSibling,
        uint FirstFile
    );

    private readonly record struct FileEntry(
        uint Name,
        uint NextFile,
        uint UserData
    );
}
