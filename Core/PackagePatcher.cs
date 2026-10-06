using STALKER2LocalizationTool.Models;

namespace STALKER2LocalizationTool.Core;

public sealed class DirectPatchInfo
{
    public long OldSerialSize { get; init; }
    public long NewSerialSize { get; init; }
    public int UexpPayloadOffset { get; init; }
}

public static class PackagePatcher
{
    public static DirectPatchInfo PatchLegacyPackage(
        string templateUasset,
        string templateUexp,
        RawExportInfo export,
        byte[] patchedPayload,
        string outputUasset,
        string sourceLabel)
    {
        if (!File.Exists(templateUasset))
            throw new FileNotFoundException($"{sourceLabel}: pristine .uasset not found", templateUasset);
        if (!File.Exists(templateUexp))
            throw new FileNotFoundException($"{sourceLabel}: pristine .uexp not found", templateUexp);
        if (export.ExportCount != 1)
            throw new InvalidDataException($"{sourceLabel}: expected exactly one export, found {export.ExportCount}");
        if (export.SerialSize != export.Payload.Length)
            throw new InvalidDataException(
                $"{sourceLabel}: RawExport SerialSize does not match payload size ({export.SerialSize} != {export.Payload.Length})"
            );

        var uexpData = File.ReadAllBytes(templateUexp);
        var first = IndexOf(uexpData, export.Payload, 0);
        if (first < 0)
            throw new InvalidDataException($"{sourceLabel}: original RawExport payload not found in .uexp");
        if (IndexOf(uexpData, export.Payload, first + 1) >= 0)
            throw new InvalidDataException($"{sourceLabel}: RawExport payload occurs more than once in .uexp");

        var newUexp = new byte[uexpData.Length - export.Payload.Length + patchedPayload.Length];
        Buffer.BlockCopy(uexpData, 0, newUexp, 0, first);
        Buffer.BlockCopy(patchedPayload, 0, newUexp, first, patchedPayload.Length);
        Buffer.BlockCopy(
            uexpData,
            first + export.Payload.Length,
            newUexp,
            first + patchedPayload.Length,
            uexpData.Length - first - export.Payload.Length
        );

        var uassetData = File.ReadAllBytes(templateUasset);
        Span<byte> signature = stackalloc byte[16];
        BinaryPrimitives.WriteInt64LittleEndian(signature[..8], export.SerialSize);
        BinaryPrimitives.WriteInt64LittleEndian(signature[8..], export.SerialOffset);

        var matches = FindAll(uassetData, signature.ToArray());
        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                $"{sourceLabel}: could not uniquely locate export SerialSize/SerialOffset in pristine .uasset " +
                $"(matches={matches.Count}, size={export.SerialSize}, offset={export.SerialOffset})"
            );
        }

        BinaryPrimitives.WriteInt64LittleEndian(uassetData.AsSpan(matches[0], 8), patchedPayload.Length);

        Directory.CreateDirectory(Path.GetDirectoryName(outputUasset)!);
        File.WriteAllBytes(outputUasset, uassetData);
        File.WriteAllBytes(Path.ChangeExtension(outputUasset, ".uexp"), newUexp);

        return new DirectPatchInfo
        {
            OldSerialSize = export.SerialSize,
            NewSerialSize = patchedPayload.Length,
            UexpPayloadOffset = first,
        };
    }

    public static void VerifyPayloadOccurrence(string uexpPath, byte[] payload, string label)
    {
        var data = File.ReadAllBytes(uexpPath);
        var first = IndexOf(data, payload, 0);
        if (first < 0 || IndexOf(data, payload, first + 1) >= 0)
            throw new InvalidDataException($"{label}: patched RawExport was not found exactly once in {uexpPath}");
    }

    public static int IndexOf(
        byte[] haystack,
        byte[] needle,
        int startIndex)
    {
        if (needle.Length == 0)
            return startIndex <= haystack.Length ? startIndex : -1;

        startIndex = Math.Max(0, startIndex);
        if (startIndex > haystack.Length - needle.Length)
            return -1;

        var relative = haystack
            .AsSpan(startIndex)
            .IndexOf(needle);

        return relative < 0
            ? -1
            : startIndex + relative;
    }

    private static List<int> FindAll(byte[] haystack, byte[] needle)
    {
        var result = new List<int>();
        var start = 0;
        while (start <= haystack.Length - needle.Length)
        {
            var pos = IndexOf(haystack, needle, start);
            if (pos < 0) break;
            result.Add(pos);
            start = pos + 1;
        }
        return result;
    }
}
