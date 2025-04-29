using System.Collections.Immutable;

namespace EasySoapClient.Contracts.Read;

public readonly record struct ReadRequest
{
    public ImmutableDictionary<string, object?> Parameters { get; }

    public ReadRequest(string name, object? value) : this([(name, value)])
    { }

    public ReadRequest(params (string Name, object? Value)[] parameters)
    {
        if (parameters is null || parameters.Length is 0)
        {
            throw new ArgumentException("At least one parameter is required.", nameof(parameters));
        }

        var builder = ImmutableDictionary.CreateBuilder<string, object?>();

        foreach ((string name, object? value) in parameters)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Parameter name cannot be null or empty.", nameof(parameters));
            }

            builder[name] = value;
        }

        Parameters = builder.ToImmutable();
    }
}
