namespace EasySoapClient.Contracts.CodeUnit;

/// <summary>
/// Describes a call to a method on a codeunit exposed as a SOAP web service.
/// </summary>
/// <param name="CodeUnitName">The service name of the codeunit, as published in NAV / Business Central.</param>
/// <param name="MethodName">The name of the codeunit method to invoke.</param>
/// <param name="Parameters">The method parameters. The sequence is copied when the request is created.</param>
public readonly record struct CodeUnitRequest(
    string CodeUnitName,
    string MethodName,
    IEnumerable<CodeUnitParameter> Parameters)
{
    private readonly CodeUnitParameter[]? _parameters = Snapshot(Parameters);

    /// <summary>
    /// The method parameters. A snapshot taken when the request was created (or changed with <c>with</c>),
    /// so later changes to the original collection do not affect the request.
    /// </summary>
    public IEnumerable<CodeUnitParameter> Parameters
    {
        get => _parameters ?? [];
        init => _parameters = Snapshot(value);
    }

    private static CodeUnitParameter[] Snapshot(IEnumerable<CodeUnitParameter>? parameters)
        => parameters is null ? [] : [.. parameters];

    /// <summary>Two requests are equal when codeunit, method and parameters (in order) are equal.</summary>
    public bool Equals(CodeUnitRequest other)
        => CodeUnitName == other.CodeUnitName
        && MethodName == other.MethodName
        && Parameters.SequenceEqual(other.Parameters);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CodeUnitName);
        hash.Add(MethodName);
        foreach (CodeUnitParameter parameter in Parameters)
        {
            hash.Add(parameter);
        }

        return hash.ToHashCode();
    }

    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append(System.Globalization.CultureInfo.InvariantCulture, $"CodeUnitName = {CodeUnitName}, MethodName = {MethodName}, Parameters = [{String.Join(", ", Parameters)}]");
        return true;
    }

    /// <summary>
    /// Creates a new request.
    /// </summary>
    public static CodeUnitRequest CreateRequest(string codeUnitName, string methodName, params IEnumerable<CodeUnitParameter> parameters)
        => new(codeUnitName, methodName, parameters);

    /// <summary>
    /// The XML namespace of the codeunit, e.g. <c>urn:microsoft-dynamics-schemas/codeunit/MyCodeunit</c>.
    /// </summary>
    public string GenerateNamespace()
        => $"urn:microsoft-dynamics-schemas/codeunit/{CodeUnitName}";

    /// <summary>
    /// The SOAP action of the method, e.g. <c>urn:microsoft-dynamics-schemas/codeunit/MyCodeunit:MyMethod</c>.
    /// </summary>
    public string GenerateSoapActionDefinedNamespace()
        => $"{GenerateNamespace()}:{MethodName}";
}

/// <summary>
/// A parameter for a codeunit method. The value is formatted the same way as page fields
/// (culture invariant, XML Schema formats), and <see langword="null"/> is sent as an empty element.
/// </summary>
public readonly record struct CodeUnitParameter(string ParameterName, object? ParameterValue);
