using System.Globalization;
using System.Text;
using System.Xml;
using EasySoapClient.Interfaces;

namespace EasySoapClient.Services;

/// <summary>
/// Removes characters that are illegal in XML 1.0. Runs in a single linear pass and only allocates
/// when something has to be removed.
/// </summary>
internal sealed class XmlSanitizerService : IXmlSanitizerService
{
    private const string CDataStart = "<![CDATA[";
    private const string CDataEnd = "]]>";
    private const string CommentStart = "<!--";
    private const string CommentEnd = "-->";

    public string RemoveIllegalCharacters(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        string result = RemoveIllegalCharacterReferences(xml);

        return RemoveIllegalRawCharacters(result);
    }

    // Character references are only interpreted outside CDATA sections and comments,
    // so those regions are copied through untouched.
    private static string RemoveIllegalCharacterReferences(string xml)
    {
        StringBuilder? builder = null;
        int copiedUpTo = 0;
        int position = 0;

        while (position < xml.Length)
        {
            int next = xml.IndexOfAny(['<', '&'], position);
            if (next < 0)
            {
                break;
            }

            if (xml[next] == '<')
            {
                position = SkipProtectedRegion(xml, next);
                continue;
            }

            // '&': a numeric character reference looks like &#123; or &#x1F; (leading zeros allowed, so any length).
            // The digits are scanned once and skipped, which keeps the pass linear.
            int digitsStart = next + 2;
            if (digitsStart > xml.Length || xml[next + 1] != '#')
            {
                position = next + 1;
                continue;
            }

            bool hex = digitsStart < xml.Length && (xml[digitsStart] == 'x' || xml[digitsStart] == 'X');
            if (hex)
            {
                digitsStart++;
            }

            int digitsEnd = digitsStart;
            while (digitsEnd < xml.Length && (hex ? Uri.IsHexDigit(xml[digitsEnd]) : char.IsAsciiDigit(xml[digitsEnd])))
            {
                digitsEnd++;
            }

            if (digitsEnd == digitsStart || digitsEnd >= xml.Length || xml[digitsEnd] != ';')
            {
                // Not a well-formed numeric reference; leave it for the parser to report.
                position = Math.Max(next + 1, digitsEnd);
                continue;
            }

            // A value that does not fit in an int is far beyond U+10FFFF, so it is illegal too.
            bool valid = TryParseCodePoint(xml.AsSpan(digitsStart, digitsEnd - digitsStart), hex, out int codePoint)
                && IsValidXmlCodePoint(codePoint);

            if (!valid)
            {
                builder ??= new StringBuilder(xml.Length);
                builder.Append(xml, copiedUpTo, next - copiedUpTo);
                copiedUpTo = digitsEnd + 1;
            }

            position = digitsEnd + 1;
        }

        if (builder is null)
        {
            return xml;
        }

        builder.Append(xml, copiedUpTo, xml.Length - copiedUpTo);
        return builder.ToString();
    }

    /// <summary>
    /// Returns the position after a CDATA section or comment starting at <paramref name="index"/>,
    /// or <c>index + 1</c> if none starts there. An unterminated region protects the rest of the document.
    /// </summary>
    private static int SkipProtectedRegion(string xml, int index)
    {
        ReadOnlySpan<char> remaining = xml.AsSpan(index);

        if (remaining.StartsWith(CDataStart, StringComparison.Ordinal))
        {
            int end = xml.IndexOf(CDataEnd, index + CDataStart.Length, StringComparison.Ordinal);
            return end < 0 ? xml.Length : end + CDataEnd.Length;
        }

        if (remaining.StartsWith(CommentStart, StringComparison.Ordinal))
        {
            int end = xml.IndexOf(CommentEnd, index + CommentStart.Length, StringComparison.Ordinal);
            return end < 0 ? xml.Length : end + CommentEnd.Length;
        }

        return index + 1;
    }

    private static bool TryParseCodePoint(ReadOnlySpan<char> digits, bool hex, out int codePoint)
        => hex
            ? int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out codePoint)
            : int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out codePoint);

    private static string RemoveIllegalRawCharacters(string xml)
    {
        StringBuilder? builder = null;

        for (int i = 0; i < xml.Length; i++)
        {
            char current = xml[i];

            if (XmlConvert.IsXmlChar(current))
            {
                builder?.Append(current);
                continue;
            }

            if (char.IsHighSurrogate(current)
                && i + 1 < xml.Length
                && XmlConvert.IsXmlSurrogatePair(xml[i + 1], current))
            {
                builder?.Append(current);
                builder?.Append(xml[i + 1]);
                i++;
                continue;
            }

            // First illegal character found: start copying, skipping this character.
            builder ??= new StringBuilder(xml, 0, i, xml.Length);
        }

        return builder?.ToString() ?? xml;
    }

    private static bool IsValidXmlCodePoint(int codePoint)
        => codePoint is 0x9 or 0xA or 0xD
        || codePoint is >= 0x20 and <= 0xD7FF
        || codePoint is >= 0xE000 and <= 0xFFFD
        || codePoint is >= 0x10000 and <= 0x10FFFF;
}
