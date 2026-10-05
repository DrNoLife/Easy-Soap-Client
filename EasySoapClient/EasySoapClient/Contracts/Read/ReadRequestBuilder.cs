namespace EasySoapClient.Contracts.Read;

/// <summary>
/// Fluent builder for <see cref="ReadRequest"/>.
/// </summary>
public sealed class ReadRequestBuilder
{
    /// <summary>Starts a new builder.</summary>
    public static ReadRequestBuilder New() => new();

    /// <summary>
    /// One-liner for the classic single-parameter case:  <br/>
    /// <c>ReadRequest req = ReadRequestBuilder.Single("10042");</c> <br/><br/>
    /// 
    /// Name defaults to "No".
    /// </summary>
#pragma warning disable CA1720 // Identifier contains type name: "Single" is part of the established public API.
    public static ReadRequest Single(object? value, string name = "No") => New()
#pragma warning restore CA1720
        .With(name, value)
        .Build();

    private readonly List<(string Name, object? Value)> _parameters = [];

    private ReadRequestBuilder() { }

    /// <summary>Adds a key field, or replaces its value (keeping its position) if it was added before.</summary>
    public ReadRequestBuilder With(string name, object? value)
    {
        if (String.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Parameter name cannot be null or empty.", nameof(name));
        }

        int existing = _parameters.FindIndex(p => p.Name == name);
        if (existing >= 0)
        {
            _parameters[existing] = (name, value);
        }
        else
        {
            _parameters.Add((name, value));
        }

        return this;
    }

    /// <summary>Adds or replaces several key fields.</summary>
    public ReadRequestBuilder With(params (string Name, object? Value)[] parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        foreach (var (name, value) in parameters)
        {
            With(name, value);
        }

        return this;
    }

    /// <summary>
    /// Builds the request.
    /// </summary>
    /// <exception cref="InvalidOperationException">No parameters have been added.</exception>
    public ReadRequest Build()
    {
        if (_parameters.Count is 0)
        {
            throw new InvalidOperationException("A <Read> request must contain at least one parameter.");
        }

        return new ReadRequest([.. _parameters]);
    }

    /// <summary>
    /// Builds the request. Explicit because building throws when no parameters have been added.
    /// </summary>
    public static explicit operator ReadRequest(ReadRequestBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Build();
    }
}
