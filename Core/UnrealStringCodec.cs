using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.Core;

public static class UnrealStringCodec
{
    public static (string Value, FStringEncoding Encoding, int Offset) ReadFString(byte[] data, int offset)
    {
        Ensure(data, offset, 4, "FString length is outside payload");
        var length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        offset += 4;

        if (length == 0)
            return (string.Empty, FStringEncoding.Ansi, offset);

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
            return (value, FStringEncoding.Wide, offset);
        }

        var narrowByteCount = length;
        Ensure(data, offset, narrowByteCount, "ANSI FString is outside payload");
        var narrow = data.AsSpan(offset, narrowByteCount);
        offset += narrowByteCount;

        if (narrowByteCount < 1 || narrow[^1] != 0)
            throw new InvalidDataException("ANSI FString is missing terminator");

        var utf8Strict = new UTF8Encoding(false, true);
        var narrowValue = utf8Strict.GetString(narrow[..^1]);
        return (narrowValue, FStringEncoding.Ansi, offset);
    }

    public static byte[] WriteFString(string? value, FStringEncoding preferredEncoding)
    {
        value ??= string.Empty;

        if (preferredEncoding == FStringEncoding.Ansi)
        {
            var raw = Encoding.UTF8.GetBytes(value);
            if (raw.All(b => b < 0x80))
            {
                var output = new byte[4 + raw.Length + 1];
                BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(0, 4), raw.Length + 1);
                raw.CopyTo(output.AsSpan(4));
                output[^1] = 0;
                return output;
            }
        }

        var wide = Encoding.Unicode.GetBytes(value);
        var codeUnitsWithTerminator = checked(wide.Length / 2 + 1);
        var result = new byte[4 + wide.Length + 2];
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(0, 4), -codeUnitsWithTerminator);
        wide.CopyTo(result.AsSpan(4));
        result[^2] = 0;
        result[^1] = 0;
        return result;
    }

    private static void Ensure(byte[] data, int offset, int length, string message)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new InvalidDataException(message);
    }
}
