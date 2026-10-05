using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using EasySoapClient.Exceptions;
using EasySoapClient.Interfaces;
using EasySoapClient.Logging;
using EasySoapClient.Models.Responses;
using EasySoapClient.Serialization;
using Microsoft.Extensions.Logging;

namespace EasySoapClient.Services;

/// <summary>
/// Parses SOAP responses directly from the response stream with <see cref="XmlReader"/>.
/// If the response contains illegal XML characters, it is read as text, sanitized and parsed once more.
/// </summary>
internal sealed partial class ParsingService(ILogger<ParsingService> logger, IXmlSanitizerService xmlSanitizer) : IParsingService
{
    // XmlSerializer instances created with an XmlRootAttribute are not cached by the runtime and each one
    // generates a new assembly that is never unloaded, so they must be cached here.
    // Lazy so concurrent first use of a type cannot build (and leak) a second serializer.
    private static readonly ConcurrentDictionary<(Type Type, string Root, string Namespace), Lazy<XmlSerializer>> Serializers = new();

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        CheckCharacters = true,
        CloseInput = false,
    };

    private readonly ILogger<ParsingService> _logger = logger;
    private readonly IXmlSanitizerService _xmlSanitizer = xmlSanitizer;

    public List<T> ParseList<T>(Stream response, string elementName, string xmlNamespace)
    {
        XmlSerializer serializer = GetSerializer(typeof(T), elementName, xmlNamespace);

        return Parse(response, reader =>
        {
            List<T> list = [];

            while (MoveToElement(reader, elementName, xmlNamespace))
            {
                list.Add((T)serializer.Deserialize(reader)!);
            }

            return list;
        });
    }

    public (bool Found, T? Value) ParseSingle<T>(Stream response, string elementName, string xmlNamespace)
    {
        XmlSerializer serializer = GetSerializer(typeof(T), elementName, xmlNamespace);

        return Parse(response, reader => MoveToElement(reader, elementName, xmlNamespace)
            ? (true, (T?)serializer.Deserialize(reader))
            : (false, default(T)));
    }

    public string? ParseResultValue(Stream response, string resultElementName)
    {
        // e.g. <GetRecIdFromKey_Result><GetRecIdFromKey_Result>Customer: 10000</GetRecIdFromKey_Result></GetRecIdFromKey_Result>
        return Parse(response, reader =>
        {
            if (!MoveToElement(reader, resultElementName, xmlNamespace: null))
            {
                return null;
            }

            var element = (XElement)XNode.ReadFrom(reader);
            return element.Value.Trim();
        });
    }

    public CodeUnitResponse ParseCodeUnitResponse(Stream response)
    {
        return Parse(response, reader =>
        {
            if (!MoveToElement(reader, "Body", SoapNames.SoapEnvelopeNamespace))
            {
                throw new SoapResponseException("The codeunit response does not contain a SOAP Body.");
            }

            var body = (XElement)XNode.ReadFrom(reader);

            // The result element (e.g. <MyMethod_Result>) is the first child of the Body.
            XElement resultElement = body.Elements().FirstOrDefault()
                ?? throw new SoapResponseException("The codeunit response does not contain a result element.");

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XElement child in resultElement.Elements())
            {
                values[child.Name.LocalName] = child.Value;
            }

            return new CodeUnitResponse(values.GetValueOrDefault("return_value", String.Empty), values);
        });
    }

    internal static XmlSerializer GetSerializer(Type type, string root, string xmlNamespace)
        => Serializers.GetOrAdd(
            (type, root, xmlNamespace),
            static key => new Lazy<XmlSerializer>(() =>
            {
                XmlSerializerSupport.EnsureSupported(key.Type);
                return new XmlSerializer(key.Type, new XmlRootAttribute(key.Root) { Namespace = key.Namespace });
            })).Value;

    /// <summary>
    /// Advances to the next element with the given name, or returns false at the end of the document.
    /// When positioned on a matching element, the reader is left there.
    /// </summary>
    private static bool MoveToElement(XmlReader reader, string localName, string? xmlNamespace)
    {
        if (reader.ReadState == ReadState.Initial)
        {
            reader.Read();
        }

        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element
                && reader.LocalName == localName
                && (xmlNamespace is null || reader.NamespaceURI == xmlNamespace))
            {
                return true;
            }

            reader.Read();
        }

        return false;
    }

    private TResult Parse<TResult>(Stream response, Func<XmlReader, TResult> read)
    {
        long start = response.Position;

        try
        {
            using XmlReader reader = XmlReader.Create(response, ReaderSettings);
            return read(reader);
        }
        catch (Exception ex) when (FindXmlException(ex) is { } xmlException)
        {
            response.Position = start;
            string xml = ReadAsText(response, xmlException);

            Log.ParseFailedRetrying(
                _logger,
                xmlException,
                xmlException.LineNumber,
                xmlException.LinePosition,
                GetXmlContext(xml, xmlException.LineNumber, xmlException.LinePosition, linesBefore: 3, linesAfter: 3));

            string sanitized = _xmlSanitizer.RemoveIllegalCharacters(xml);

            try
            {
                using var textReader = new StringReader(sanitized);
                using XmlReader reader = XmlReader.Create(textReader, ReaderSettings);
                TResult result = read(reader);

                Log.ParseSucceededAfterSanitizing(_logger);
                return result;
            }
            catch (Exception retryException) when (FindXmlException(retryException) is { } retryXmlException)
            {
                Log.ParseFailedAfterSanitizing(
                    _logger,
                    retryXmlException,
                    retryXmlException.LineNumber,
                    retryXmlException.LinePosition,
                    GetXmlContext(sanitized, retryXmlException.LineNumber, retryXmlException.LinePosition, linesBefore: 3, linesAfter: 3));

                // The message already contains the line and position; passing them again would repeat them.
                throw new XmlException(
                    $"The SOAP response is not valid XML, even after removing illegal characters: {retryXmlException.Message}",
                    xmlException);
            }
        }
    }

    // XmlSerializer wraps reader errors in InvalidOperationException.
    private static XmlException? FindXmlException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is XmlException xmlException)
            {
                return xmlException;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the response as text, honouring a byte order mark or the encoding in the XML declaration
    /// (defaulting to UTF-8, like the XML parser).
    /// </summary>
    private static string ReadAsText(Stream stream, XmlException originalError)
    {
        long start = stream.Position;
        Span<byte> head = stackalloc byte[1024];
        int read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        stream.Position = start;

        Encoding encoding = DeclaredEncoding(head[..read], originalError) ?? Encoding.UTF8;

        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static Encoding? DeclaredEncoding(ReadOnlySpan<byte> head, XmlException originalError)
    {
        string text = Encoding.ASCII.GetString(head);
        if (!text.StartsWith("<?xml", StringComparison.Ordinal))
        {
            return null;
        }

        int declarationEnd = text.IndexOf("?>", StringComparison.Ordinal);
        if (declarationEnd < 0)
        {
            throw new XmlException("The SOAP response could not be parsed, and the encoding could not be read from its XML declaration (incomplete or too long).", originalError);
        }

        Match match = EncodingDeclarationRegex().Match(text, 0, declarationEnd);
        if (!match.Success)
        {
            return null;
        }

        string name = match.Groups[1].Value;
        Encoding? encoding = TryGetEncoding(name);

        // Decoding with a guessed encoding would silently corrupt text, so give up instead.
        if (encoding is null)
        {
            throw new XmlException($"The SOAP response could not be parsed, and its declared encoding '{name}' is not supported for the illegal-character fallback.", originalError);
        }

        // The declaration was readable as single bytes, so the document is ASCII compatible: a declared
        // UTF-16/32 contradicts that (a BOM, if any, is still honoured by the reader). Read it as UTF-8.
        return encoding.GetByteCount("<") == 1 ? encoding : null;
    }

    private static Encoding? TryGetEncoding(string name)
    {
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            // Windows code pages (e.g. windows-1252) are not registered by default on .NET;
            // obsolete encodings (e.g. utf-7) are not supported at all and end up as null.
            return CodePagesEncodingProvider.Instance.GetEncoding(name);
        }
    }

    [GeneratedRegex("encoding\\s*=\\s*[\"']([A-Za-z0-9._-]+)[\"']")]
    private static partial Regex EncodingDeclarationRegex();

    /// <summary>
    /// A few lines around the error, each cut to a window of characters around the error position.
    /// NAV usually returns the whole response on one line, so whole lines would log the whole response.
    /// </summary>
    private static string GetXmlContext(string xml, int targetLine, int targetPosition, int linesBefore, int linesAfter)
    {
        int startLine = Math.Max(1, targetLine - linesBefore);
        int endLine = targetLine + linesAfter;

        using StringReader reader = new(xml);
        StringBuilder builder = new();
        int currentLine = 1;

        while (reader.ReadLine() is { } line)
        {
            if (currentLine > endLine)
            {
                break;
            }

            if (currentLine >= startLine)
            {
                bool isTarget = currentLine == targetLine;
                builder.Append(isTarget ? ">>>" : "   ");
                builder.Append(' ');
                builder.Append(currentLine);
                builder.Append(": ");
                builder.AppendLine(EscapeControlCharacters(Window(line, isTarget ? targetPosition : 1)));
            }

            currentLine++;
        }

        return builder.ToString();
    }

    private const int ContextCharacters = 120;

    private static string Window(string line, int position)
    {
        if (line.Length <= ContextCharacters * 2)
        {
            return line;
        }

        int start = Math.Clamp(position - 1 - ContextCharacters, 0, line.Length);
        int end = Math.Min(line.Length, start + (ContextCharacters * 2));

        return (start > 0 ? "…" : String.Empty) + line[start..end] + (end < line.Length ? "…" : String.Empty);
    }

    private static string EscapeControlCharacters(string value)
    {
        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            if (char.IsControl(character) && character is not '\r' and not '\n' and not '\t')
            {
                builder.Append("\\u");
                builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
