namespace EasySoapClient.Contracts.CodeUnit;

/// <summary>
/// Fluent builder for <see cref="CodeUnitRequest"/>.
/// </summary>
public static class CodeUnitRequestBuilder
{
    /// <summary>
    /// Starts a new builder for the given codeunit.
    /// </summary>
    public static Builder WithCodeUnit(string codeUnitName)
        => new Builder().WithCodeUnit(codeUnitName);

    /// <summary>
    /// The builder returned by <see cref="WithCodeUnit(string)"/>.
    /// </summary>
    public sealed class Builder
    {
        private string _codeUnitName = String.Empty;
        private string _methodName = String.Empty;
        private readonly List<CodeUnitParameter> _parameters = [];

        /// <summary>Sets the codeunit service name.</summary>
        public Builder WithCodeUnit(string codeUnitName)
        {
            _codeUnitName = codeUnitName;
            return this;
        }

        /// <summary>Sets the method to invoke.</summary>
        public Builder WithMethod(string methodName)
        {
            _methodName = methodName;
            return this;
        }

        /// <summary>Adds a parameter.</summary>
        public Builder AddParameter(string parameterName, object? parameterValue)
        {
            _parameters.Add(new CodeUnitParameter(parameterName, parameterValue));
            return this;
        }

        /// <summary>
        /// Builds the request. The builder can be reused afterwards without affecting requests already built.
        /// </summary>
        public CodeUnitRequest Build() =>
            new(_codeUnitName, _methodName, [.. _parameters]);
    }
}
