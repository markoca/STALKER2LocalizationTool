using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Core;

public static class LocalizationDatabaseCodec
{
    public static LocalizationPayload Parse(byte[] data, string sourceLabel)
    {
        if (data.Length < 10)
            throw new InvalidDataException($"{sourceLabel}: localization payload is too small");

        var offset = 0;
        var rootHeader = data.AsSpan(offset, 6).ToArray();
        offset += 6;

        var recordCount = ReadInt32(data, ref offset, sourceLabel + ": record count");
        if (recordCount < 0 || recordCount > 100000)
            throw new InvalidDataException($"{sourceLabel}: unreasonable localization record count {recordCount}");

        var payload = new LocalizationPayload { RootHeader = rootHeader };

        for (var recordIndex = 0; recordIndex < recordCount; recordIndex++)
        {
            var sidResult = UnrealStringCodec.ReadFString(data, offset);
            offset = sidResult.Offset;
            if (string.IsNullOrEmpty(sidResult.Value))
                throw new InvalidDataException($"{sourceLabel}: empty SID at record {recordIndex}");

            Ensure(data, offset, 10, $"{sourceLabel}: truncated record {recordIndex} ({sidResult.Value})");
            var nestedHeader = data.AsSpan(offset, 6).ToArray();
            offset += 6;

            var translationCount = ReadInt32(data, ref offset, sourceLabel + ": translation count");
            if (translationCount < 0 || translationCount > 256)
                throw new InvalidDataException(
                    $"{sourceLabel}: unreasonable translation count {translationCount} for {sidResult.Value}"
                );

            var record = new LocalizationRecord
            {
                Sid = sidResult.Value,
                SidEncoding = sidResult.Encoding,
                NestedHeader = nestedHeader,
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
                    Encoding = valueResult.Encoding,
                });
            }

            payload.Records.Add(record);
        }

        payload.Trailer = data.AsSpan(offset).ToArray();
        return payload;
    }

    public static byte[] Serialize(LocalizationPayload parsed)
    {
        using var stream = new MemoryStream();
        WritePayload(stream, parsed);
        return stream.ToArray();
    }

    public static bool RoundTripMatches(
        LocalizationPayload parsed,
        byte[] originalPayload)
    {
        using var stream = new ComparingWriteStream(originalPayload);
        WritePayload(stream, parsed);
        return stream.IsExactMatch;
    }

    private static void WritePayload(
        Stream stream,
        LocalizationPayload parsed)
    {
        stream.Write(parsed.RootHeader);
        WriteInt32(stream, parsed.Records.Count);

        foreach (var record in parsed.Records)
        {
            UnrealStringCodec.WriteFString(
                stream,
                record.Sid,
                record.SidEncoding
            );
            stream.Write(record.NestedHeader);
            WriteInt32(stream, record.Translations.Count);

            foreach (var translation in record.Translations)
            {
                WriteInt64(stream, translation.LanguageId);
                UnrealStringCodec.WriteFString(
                    stream,
                    translation.Value,
                    translation.Encoding
                );
            }
        }

        stream.Write(parsed.Trailer);
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

        var result = new PatchResult
        {
            RecordCount = recordCount,
        };

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
                result.AlreadyCorrectSids.Add(sidResult.Value);

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

    private sealed class ComparingWriteStream : Stream
    {
        private readonly byte[] _expected;
        private int _offset;
        private bool _matches = true;

        public ComparingWriteStream(byte[] expected)
        {
            _expected = expected;
        }

        public bool IsExactMatch =>
            _matches && _offset == _expected.Length;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _offset;

        public override long Position
        {
            get => _offset;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_offset > _expected.Length - buffer.Length)
            {
                _matches = false;
                _offset += buffer.Length;
                return;
            }

            if (_matches
                && !buffer.SequenceEqual(
                    _expected.AsSpan(_offset, buffer.Length)))
            {
                _matches = false;
            }

            _offset += buffer.Length;
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();
    }

    private static int ReadInt32(byte[] data, ref int offset, string label)
    {
        Ensure(data, offset, 4, label + " is outside payload");
        var value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        offset += 4;
        return value;
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void Ensure(byte[] data, int offset, int length, string message)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new InvalidDataException(message);
    }
}
