using STALKER2LocalizationTool.Models;

namespace STALKER2LocalizationTool.Core;

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
            stream.Write(
                UnrealStringCodec.WriteFString(
                    record.Sid,
                    record.SidEncoding
                )
            );
            stream.Write(record.NestedHeader);
            WriteInt32(stream, record.Translations.Count);

            foreach (var translation in record.Translations)
            {
                Span<byte> idBytes = stackalloc byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(
                    idBytes,
                    translation.LanguageId
                );
                stream.Write(idBytes);
                stream.Write(
                    UnrealStringCodec.WriteFString(
                        translation.Value,
                        translation.Encoding
                    )
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
        var parsed = Parse(originalPayload, sourceLabel);
        var result = new PatchResult { RecordCount = parsed.Records.Count };

        foreach (var record in parsed.Records)
        {
            if (!translations.TryGetValue(record.Sid, out var replacement) || string.IsNullOrEmpty(replacement))
                continue;

            result.MatchedSids.Add(record.Sid);
            var target = record.Translations.FirstOrDefault(x => x.LanguageId == targetLanguageId);

            if (target is null)
            {
                record.Translations.Add(new LocalizationTranslation
                {
                    LanguageId = targetLanguageId,
                    Value = replacement,
                    Encoding = FStringEncoding.Wide,
                });
                result.ChangedSids.Add(record.Sid);
                result.ExpectedValues[record.Sid] = replacement;
                continue;
            }

            if (string.Equals(target.Value, replacement, StringComparison.Ordinal))
            {
                result.AlreadyCorrectSids.Add(record.Sid);
                continue;
            }

            target.Value = replacement;
            target.Encoding = FStringEncoding.Wide;
            result.ChangedSids.Add(record.Sid);
            result.ExpectedValues[record.Sid] = replacement;
        }

        result.Payload = Serialize(parsed);

        if (result.ChangedSids.Count == 0 && !result.Payload.AsSpan().SequenceEqual(originalPayload))
            throw new InvalidDataException($"{sourceLabel}: parser changed an untouched localization payload");

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

    private static void Ensure(byte[] data, int offset, int length, string message)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new InvalidDataException(message);
    }
}
