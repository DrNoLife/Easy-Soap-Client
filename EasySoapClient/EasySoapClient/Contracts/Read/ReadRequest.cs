namespace EasySoapClient.Contracts.Read;

/// <summary>
/// The key fields for a <c>Read</c> call. The parameter names are the element names of the page's key
/// fields (e.g. <c>No</c>, or <c>Document_Type</c> + <c>No</c> for a compound key).
/// </summary>
public readonly record struct ReadRequest
{
    /// <summary>
    /// The key fields, in the order they were given (the order of the page's key in the WSDL).
    /// Values are formatted culture invariant using XML Schema formats.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object?>> Parameters { get; }

    /// <summary>Creates a request with a single key field.</summary>
    public ReadRequest(string name, object? value) : this([(name, value)])
    { }

    /// <summary>Creates a request with one or more key fields.</summary>
    public ReadRequest(params (string Name, object? Value)[] parameters)
    {
        if (parameters is null || parameters.Length is 0)
        {
            throw new ArgumentException("At least one parameter is required.", nameof(parameters));
        }

        var ordered = new List<KeyValuePair<string, object?>>(parameters.Length);

        foreach ((string name, object? value) in parameters)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Parameter name cannot be null or empty.", nameof(parameters));
            }

            // A repeated name replaces the earlier value, keeping its position.
            int existing = ordered.FindIndex(p => p.Key == name);
            if (existing >= 0)
            {
                ordered[existing] = new(name, value);
            }
            else
            {
                ordered.Add(new(name, value));
            }
        }

        Parameters = ordered.AsReadOnly();
    }

    /// <summary>Two requests are equal when they have the same key fields with equal values, in the same order.</summary>
    public bool Equals(ReadRequest other)
    {
        IReadOnlyList<KeyValuePair<string, object?>> mine = Parameters ?? [];
        IReadOnlyList<KeyValuePair<string, object?>> theirs = other.Parameters ?? [];

        if (mine.Count != theirs.Count)
        {
            return false;
        }

        for (int i = 0; i < mine.Count; i++)
        {
            if (mine[i].Key != theirs[i].Key || !Equals(mine[i].Value, theirs[i].Value))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (KeyValuePair<string, object?> parameter in Parameters ?? [])
        {
            hash.Add(parameter.Key);
            hash.Add(parameter.Value);
        }

        return hash.ToHashCode();
    }
}
