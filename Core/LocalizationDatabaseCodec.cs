using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Core;

public static class LocalizationDatabaseCodec
{
    public static LocalizationPayload Parse(byte[] data, string sourceLabel)
    {
        if (data.Length < 10)
            throw new InvalidDataException($"{sourceLabel}: localization payload is too small");

        var offset = 0;
        offset += 6;

        var recordCount = ReadInt32(data, ref offset, sourceLabel + ": record count");
        if (recordCount < 0 || recordCount > 100000)
            throw new InvalidDataException($"{sourceLabel}: unreasonable localization record count {recordCount}");

        var payload = new LocalizationPayload();

        for (var recordIndex = 0; recordIndex < recordCount; recordIndex++)
        {
            var sidResult = UnrealStringCodec.ReadFString(data, offset);
            offset = sidResult.Offset;
            if (string.IsNullOrEmpty(sidResult.Value))
                throw new InvalidDataException($"{sourceLabel}: empty SID at record {recordIndex}");

            Ensure(data, offset, 10, $"{sourceLabel}: truncated record {recordIndex} ({sidResult.Value})");
            offset += 6;

            var translationCount = ReadInt32(data, ref offset, sourceLabel + ": translation count");
            if (translationCount < 0 || translationCount > 256)
                throw new InvalidDataException(
                    $"{sourceLabel}: unreasonable translation count {translationCount} for {sidResult.Value}"
                );

            var record = new LocalizationRecord
            {
                Sid = sidResult.Value,
            };

            for (var i = 0; i < translationCount; i++)
            {
                Ensure(data, offset, 8, $"{sourceLabel}: truncated language id for {sidResult.Value}");
                var languageId = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(offset, 8));
                offset += 8;

                var valueResult = UnrealStringCodec.ReadFString(data, offset);
                offset = valueResult.Offset;
                record.Translations.Add(new LocalizationTranslation
                {
                    LanguageId = languageId,
                    Value = valueResult.Value,
                });
            }

            payload.Records.Add(record);
        }

        return payload;
    }

    public static PatchResult Patch(
        byte[] originalPayload,
        IReadOnlyDictionary<string, string> translations,
        int targetLanguageId,
        string sourceLabel)
    {
        if (originalPayload.Length < 10)
        {
            throw new InvalidDataException(
                $"{sourceLabel}: localization payload is too small"
            );
        }

        var offset = 6;
        var recordCount = ReadInt32(
            originalPayload,
            ref offset,
            sourceLabel + ": record count"
        );
        if (recordCount < 0 || recordCount > 100000)
        {
            throw new InvalidDataException(
                $"{sourceLabel}: unreasonable localization record count "
                + $"{recordCount}"
            );
        }

        var result = new PatchResult();

        MemoryStream? output = null;

        void EnsureOutput(int unchangedPrefixLength)
        {
            if (output is not null)
                return;

            output = new MemoryStream(originalPayload.Length);
            output.Write(
                originalPayload.AsSpan(0, unchangedPrefixLength)
            );
        }

        for (var recordIndex = 0;
             recordIndex < recordCount;
             recordIndex++)
        {
            var recordStart = offset;

            var sidResult = UnrealStringCodec.ReadFString(
                originalPayload,
                offset
            );
            offset = sidResult.Offset;
            if (string.IsNullOrEmpty(sidResult.Value))
            {
                throw new InvalidDataException(
                    $"{sourceLabel}: empty SID at record {recordIndex}"
                );
            }

            Ensure(
                originalPayload,
                offset,
                10,
                $"{sourceLabel}: truncated record {recordIndex} "
                + $"({sidResult.Value})"
            );

            offset += 6; // nested header
            var translationCountOffset = offset;
            var translationCount = ReadInt32(
                originalPayload,
                ref offset,
                sourceLabel + ": translation count"
            );
            if (translationCount < 0 || translationCount > 256)
            {
                throw new InvalidDataException(
                    $"{sourceLabel}: unreasonable translation count "
                    + $"{translationCount} for {sidResult.Value}"
                );
            }

            var translationsStart = offset;
            var hasReplacement = translations.TryGetValue(
                sidResult.Value,
                out var replacement
            ) && !string.IsNullOrEmpty(replacement);

            var targetFound = false;
            string? targetValue = null;
            var targetValueStart = -1;
            var targetValueEnd = -1;

            for (var i = 0; i < translationCount; i++)
            {
                Ensure(
                    originalPayload,
                    offset,
                    8,
                    $"{sourceLabel}: truncated language id for "
                    + sidResult.Value
                );

                var languageId = BinaryPrimitives.ReadInt64LittleEndian(
                    originalPayload.AsSpan(offset, 8)
                );
                offset += 8;

                var valueStart = offset;

                if (hasReplacement
                    && !targetFound
                    && languageId == targetLanguageId)
                {
                    var valueResult = UnrealStringCodec.ReadFString(
                        originalPayload,
                        offset
                    );
                    offset = valueResult.Offset;

                    targetFound = true;
                    targetValue = valueResult.Value;
                    targetValueStart = valueStart;
                    targetValueEnd = offset;
                }
                else
                {
                    offset = UnrealStringCodec.SkipFString(
                        originalPayload,
                        offset
                    );
                }
            }

            var recordEnd = offset;

            if (!hasReplacement)
            {
                if (output is not null)
                {
                    output.Write(
                        originalPayload.AsSpan(
                            recordStart,
                            recordEnd - recordStart
                        )
                    );
                }
                continue;
            }

            result.MatchedSids.Add(sidResult.Value);

            if (targetFound
                && string.Equals(
                    targetValue,
                    replacement,
                    StringComparison.Ordinal))
            {
                if (output is not null)
                {
                    output.Write(
                        originalPayload.AsSpan(
                            recordStart,
                            recordEnd - recordStart
                        )
                    );
                }
                continue;
            }

            result.ChangedSids.Add(sidResult.Value);
            result.ExpectedValues[sidResult.Value] = replacement!;

            EnsureOutput(recordStart);

            if (targetFound)
            {
                output!.Write(
                    originalPayload.AsSpan(
                        recordStart,
                        targetValueStart - recordStart
                    )
                );
                UnrealStringCodec.WriteFString(
                    output,
                    replacement,
                    FStringEncoding.Wide
                );
                output.Write(
                    originalPayload.AsSpan(
                        targetValueEnd,
                        recordEnd - targetValueEnd
                    )
                );
            }
            else
            {
                output!.Write(
                    originalPayload.AsSpan(
                        recordStart,
                        translationCountOffset - recordStart
                    )
                );
                WriteInt32(output, checked(translationCount + 1));
                output.Write(
                    originalPayload.AsSpan(
                        translationsStart,
                        recordEnd - translationsStart
                    )
                );

                WriteInt64(output, targetLanguageId);
                UnrealStringCodec.WriteFString(
                    output,
                    replacement,
                    FStringEncoding.Wide
                );
            }
        }

        if (output is null)
        {
            // No changed value means no rewrite at all: preserve the original
            // payload object byte-for-byte without a second serialization pass.
            result.Payload = originalPayload;
            return result;
        }

        output.Write(
            originalPayload.AsSpan(
                offset,
                originalPayload.Length - offset
            )
        );
        result.Payload = output.ToArray();
        output.Dispose();

        return result;
    }

    public static void VerifyExpectedValues(byte[] payload, int targetLanguageId, IReadOnlyDictionary<string, string> expected, string label)
    {
        var parsed = Parse(payload, label);
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var record in parsed.Records)
        {
            var target = record.Translations.FirstOrDefault(x => x.LanguageId == targetLanguageId);
            if (target is not null)
                actual[record.Sid] = target.Value;
        }

        var failures = expected
            .Where(pair => !actual.TryGetValue(pair.Key, out var value) || value != pair.Value)
            .Take(20)
            .Select(pair => $"{pair.Key}: expected={pair.Value}, actual={(actual.TryGetValue(pair.Key, out var value) ? value : "<missing>")}")
            .ToList();

        if (failures.Count > 0)
            throw new InvalidDataException(label + ": patched localization verification failed\r\n" + string.Join("\r\n", failures));
    }


    private static int ReadInt32(byte[] data, ref int offset, string label)
    {
        Ensure(data, offset, 4, label);
        var value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        offset += 4;
        return value;
    }

    private static void Ensure(byte[] data, int offset, int length, string label)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new InvalidDataException(label);
    }

    private static void WriteInt32(Stream output, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        output.Write(bytes);
    }

    private static void WriteInt64(Stream output, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        output.Write(bytes);
    }


}
