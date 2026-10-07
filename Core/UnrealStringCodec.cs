using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Core;

public static class UnrealStringCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public static (string Value, int Offset) ReadFString(byte[] data, int offset)
    {
        Ensure(data, offset, 4, "FString length is outside payload");
        var length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        offset += 4;

        if (length == 0)
            return (string.Empty, offset);

        if (length < 0)
        {
            var charCount = checked(-length);
            var byteCount = checked(charCount * 2);
            Ensure(data, offset, byteCount, "UTF-16 FString is outside payload");
            var raw = data.AsSpan(offset, byteCount);
            offset += byteCount;

            if (byteCount < 2 || raw[^2] != 0 || raw[^1] != 0)
                throw new InvalidDataException("UTF-16 FString is missing terminator");

            var value = Encoding.Unicode.GetString(raw[..^2]);
            return (value, offset);
        }

        var narrowByteCount = length;
        Ensure(data, offset, narrowByteCount, "ANSI FString is outside payload");
        var narrow = data.AsSpan(offset, narrowByteCount);
        offset += narrowByteCount;

        if (narrowByteCount < 1 || narrow[^1] != 0)
            throw new InvalidDataException("ANSI FString is missing terminator");

        var narrowValue = StrictUtf8.GetString(narrow[..^1]);
        return (narrowValue, offset);
    }

    public static int SkipFString(byte[] data, int offset)
    {
        Ensure(data, offset, 4, "FString length is outside payload");
        var length = BinaryPrimitives.ReadInt32LittleEndian(
            data.AsSpan(offset, 4)
        );
        offset += 4;

        if (length == 0)
            return offset;

        if (length < 0)
        {
            var charCount = checked(-length);
            var byteCount = checked(charCount * 2);
            Ensure(
                data,
                offset,
                byteCount,
                "UTF-16 FString is outside payload"
            );

            var end = offset + byteCount;
            if (byteCount < 2
                || data[end - 2] != 0
                || data[end - 1] != 0)
            {
                throw new InvalidDataException(
                    "UTF-16 FString is missing terminator"
                );
            }

            return end;
        }

        Ensure(
            data,
            offset,
            length,
            "ANSI FString is outside payload"
        );

        var ansiEnd = offset + length;
        if (length < 1 || data[ansiEnd - 1] != 0)
        {
            throw new InvalidDataException(
                "ANSI FString is missing terminator"
            );
        }

        return ansiEnd;
    }

    public static void WriteFString(
        Stream stream,
        string? value,
        FStringEncoding preferredEncoding)
    {
        value ??= string.Empty;

        if (preferredEncoding == FStringEncoding.Ansi)
        {
            var byteCount = Encoding.UTF8.GetByteCount(value);
            var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(
                Math.Max(1, byteCount)
            );
            try
            {
                var written = Encoding.UTF8.GetBytes(
                    value.AsSpan(),
                    rented.AsSpan(0, byteCount)
                );

                var asciiOnly = true;
                for (var i = 0; i < written; i++)
                {
                    if (rented[i] >= 0x80)
                    {
                        asciiOnly = false;
                        break;
                    }
                }

                if (asciiOnly)
                {
                    WriteLength(stream, written + 1);
                    stream.Write(rented, 0, written);
                    stream.WriteByte(0);
                    return;
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }

        var wideByteCount = Encoding.Unicode.GetByteCount(value);
        var wideRented = System.Buffers.ArrayPool<byte>.Shared.Rent(
            Math.Max(2, wideByteCount)
        );
        try
        {
            var written = Encoding.Unicode.GetBytes(
                value.AsSpan(),
                wideRented.AsSpan(0, wideByteCount)
            );

            var codeUnitsWithTerminator = checked(written / 2 + 1);
            WriteLength(stream, -codeUnitsWithTerminator);
            stream.Write(wideRented, 0, written);
            stream.WriteByte(0);
            stream.WriteByte(0);
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(wideRented);
        }
    }

    private static void WriteLength(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    public static byte[] WriteFString(
        string? value,
        FStringEncoding preferredEncoding)
    {
        using var stream = new MemoryStream();
        WriteFString(stream, value, preferredEncoding);
        return stream.ToArray();
    }

    private static void Ensure(byte[] data, int offset, int length, string message)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new InvalidDataException(message);
    }
}
