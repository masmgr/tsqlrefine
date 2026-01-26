using System.Text;
using UtfUnknown;

namespace TsqlRefine.Cli;

internal static class CharsetDetection
{
    public sealed record DecodedText(string Text, Encoding ReadEncoding, Encoding WriteEncoding);

    public static async Task<DecodedText> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return Decode(bytes);
    }

    public static async Task<DecodedText> ReadStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return Decode(ms.ToArray());
    }

    public static DecodedText Decode(byte[] bytes)
    {
        EnsureEncodingProvidersRegistered();

        if (bytes.Length == 0)
        {
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            return new DecodedText(string.Empty, utf8NoBom, utf8NoBom);
        }

        if (TryDetectBomEncoding(bytes, out var bomEncoding))
        {
            var preambleLength = bomEncoding.GetPreamble().Length;
            var text = preambleLength > 0 && bytes.Length >= preambleLength
                ? bomEncoding.GetString(bytes, preambleLength, bytes.Length - preambleLength)
                : bomEncoding.GetString(bytes);
            var writeEncoding = CreateWriteEncoding(bomEncoding, emitBom: true);
            return new DecodedText(text, bomEncoding, writeEncoding);
        }

        var detection = CharsetDetector.DetectFromBytes(bytes);
        var detectedEncoding = detection.Detected?.Encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var decoded = detectedEncoding.GetString(bytes);

        if (ShouldPreferShiftJis(detectedEncoding))
        {
            var shiftJis = Encoding.GetEncoding(932);
            var shiftJisDecoded = shiftJis.GetString(bytes);
            if (ContainsJapaneseCharacters(shiftJisDecoded) && !ContainsJapaneseCharacters(decoded))
            {
                detectedEncoding = shiftJis;
                decoded = shiftJisDecoded;
            }
        }

        var write = CreateWriteEncoding(detectedEncoding, emitBom: false);
        return new DecodedText(decoded, detectedEncoding, write);
    }

    private static Encoding CreateWriteEncoding(Encoding encoding, bool emitBom)
    {
        if (encoding is UTF8Encoding)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom);
        }

        if (encoding is UnicodeEncoding unicode)
        {
            var isBigEndian = unicode.CodePage == Encoding.BigEndianUnicode.CodePage;
            return new UnicodeEncoding(bigEndian: isBigEndian, byteOrderMark: emitBom);
        }

        if (encoding is UTF32Encoding utf32)
        {
            var isBigEndian = utf32.CodePage == Encoding.GetEncoding(12001).CodePage;
            return new UTF32Encoding(bigEndian: isBigEndian, byteOrderMark: emitBom);
        }

        return encoding;
    }

    private static bool TryDetectBomEncoding(byte[] bytes, out Encoding encoding)
    {
        // UTF-8 BOM: EF BB BF
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return true;
        }

        // UTF-32 LE BOM: FF FE 00 00
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true);
            return true;
        }

        // UTF-32 BE BOM: 00 00 FE FF
        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
            return true;
        }

        // UTF-16 LE BOM: FF FE
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            return true;
        }

        // UTF-16 BE BOM: FE FF
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
            return true;
        }

        encoding = Encoding.UTF8;
        return false;
    }

    private static bool ShouldPreferShiftJis(Encoding detectedEncoding)
    {
        var webName = detectedEncoding.WebName;
        return string.Equals(webName, "windows-1252", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(webName, "iso-8859-1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(webName, "us-ascii", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsJapaneseCharacters(string text)
    {
        foreach (var c in text)
        {
            // Hiragana, Katakana, Halfwidth Katakana, CJK Unified Ideographs
            if ((c >= '\u3040' && c <= '\u309F') ||
                (c >= '\u30A0' && c <= '\u30FF') ||
                (c >= '\uFF61' && c <= '\uFF9F') ||
                (c >= '\u4E00' && c <= '\u9FFF'))
            {
                return true;
            }
        }

        return false;
    }

    private static int _encodingProvidersRegistered;

    private static void EnsureEncodingProvidersRegistered()
    {
        if (Interlocked.Exchange(ref _encodingProvidersRegistered, 1) == 1)
        {
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
