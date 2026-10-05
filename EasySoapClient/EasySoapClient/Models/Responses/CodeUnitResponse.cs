namespace EasySoapClient.Models.Responses;

/// <summary>
/// The result of a codeunit call.
/// </summary>
/// <param name="Value">The <c>return_value</c> element, or an empty string if the method has no return value.</param>
/// <param name="Values">
/// Every element returned by the method, keyed by element name. This includes <c>return_value</c>
/// and any by-reference (<c>var</c>) parameters.
/// </param>
public sealed record CodeUnitResponse(string Value, IReadOnlyDictionary<string, string> Values)
{
    /// <summary>Creates a response that only holds a return value.</summary>
    public CodeUnitResponse(string Value)
        : this(Value, new Dictionary<string, string> { ["return_value"] = Value })
    { }
}
