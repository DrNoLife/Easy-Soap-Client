namespace EasySoapClient.Contracts.CodeUnit;

public static class CodeUnitRequestBuilder
{
    public static Builder WithCodeUnit(string codeUnitName)
        => new Builder().WithCodeUnit(codeUnitName);

    public class Builder
    {
        private string _codeUnitName = String.Empty;
        private string _methodName = String.Empty;
        private readonly List<CodeUnitParameter> _parameters = [];

        public Builder WithCodeUnit(string codeUnitName)
        {
            _codeUnitName = codeUnitName;
            return this;
        }

        public Builder WithMethod(string methodName)
        {
            _methodName = methodName;
            return this;
        }

        public Builder AddParameter(string parameterName, string parameterValue)
        {
            _parameters.Add(new CodeUnitParameter(parameterName, parameterValue));
            return this;
        }

        public CodeUnitRequest Build() =>
            new(_codeUnitName, _methodName, _parameters);
    }
}

