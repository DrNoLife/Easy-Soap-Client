namespace EasySoapClient.Contracts.Read;

public sealed class ReadRequestBuilder
{
    public static ReadRequestBuilder New() => new();

    /// <summary>
    /// One-liner for the classic single-parameter case:  <br/>
    /// <c>ReadRequest req = ReadRequestBuilder.Single("10042");</c> <br/><br/>
    /// 
    /// Name defaults to "No".
    /// </summary>
    public static ReadRequest Single(object? value, string name = "No") => New()
        .With(name, value)
        .Build();

    private readonly List<(string Name, object? Value)> _parameters = [];

    private ReadRequestBuilder() { }

    public ReadRequestBuilder With(string name, object? value)
    {
        if (String.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Parameter name cannot be null or empty.", nameof(name));
        }

        _parameters.RemoveAll(p => p.Name == name); 
        _parameters.Add((name, value));
        return this;
    }

    public ReadRequestBuilder With(params (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
        {
            With(name, value);
        }

        return this;
    }

    public ReadRequest Build()
    {
        if (_parameters.Count is 0)
        {
            throw new InvalidOperationException("A <Read> request must contain at least one parameter.");
        }

        return new ReadRequest([.. _parameters]);
    }

    public static implicit operator ReadRequest(ReadRequestBuilder builder) => builder.Build();

}
