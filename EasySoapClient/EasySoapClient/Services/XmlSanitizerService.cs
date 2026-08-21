using EasySoapClient.Interfaces;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace EasySoapClient.Services;

public partial class XmlSanitizerService : IXmlSanitizerService
{
    [GeneratedRegex("&#(?:[xX]([0-9A-Fa-f]+)|([0-9]+));")]
    private static partial Regex NumericCharacterReferenceRegex();

    [GeneratedRegex(@"<!\[CDATA\[.*?\]\]>|<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CDataAndCommentRegex();

    public string RemoveIllegalCharacters(string xml)
    {
        string result = RemoveIllegalCharacterReferences(xml);

        return RemoveIllegalRawCharacters(result);
    }

    // Character references are only interpreted outside CDATA sections and comments,
    // so those regions are copied through untouched.
    private static string RemoveIllegalCharacterReferences(string xml)
    {
        MatchCollection protectedRegions = CDataAndCommentRegex().Matches(xml);

        if (protectedRegions.Count == 0)
        {
            return NumericCharacterReferenceRegex().Replace(xml, ReplaceIllegalCharacterReference);
        }

        StringBuilder builder = new(xml.Length);
        int position = 0;

        foreach (Match region in protectedRegions)
        {
            builder.Append(NumericCharacterReferenceRegex().Replace(xml[position..region.Index], ReplaceIllegalCharacterReference));
            builder.Append(xml, region.Index, region.Length);
            position = region.Index + region.Length;
        }

        builder.Append(NumericCharacterReferenceRegex().Replace(xml[position..], ReplaceIllegalCharacterReference));

        return builder.ToString();
    }

    private static string ReplaceIllegalCharacterReference(Match match)
    {
        string hexValue = match.Groups[1].Value;
        string decimalValue = match.Groups[2].Value;

        int codePoint;
        bool parsed = hexValue.Length > 0
            ? int.TryParse(hexValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codePoint)
            : int.TryParse(decimalValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out codePoint);

        return parsed && IsValidXmlCodePoint(codePoint)
            ? match.Value
            : string.Empty;
    }

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
