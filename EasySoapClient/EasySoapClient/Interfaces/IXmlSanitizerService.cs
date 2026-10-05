namespace EasySoapClient.Interfaces;

internal interface IXmlSanitizerService
{
    /// <summary>
    /// Removes characters that are illegal in XML 1.0 from the given string,
    /// whether they appear as raw characters or as numeric character references (e.g. &amp;#x1F;).
    /// </summary>
    string RemoveIllegalCharacters(string xml);
}
