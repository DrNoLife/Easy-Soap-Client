namespace EasySoapClient.Contracts.CodeUnit;

public record CodeUnitRequest(
    string CodeUnitName,
    string MethodName,
    IEnumerable<CodeUnitParameter> Parameters)
{
    public static CodeUnitRequest CreateRequest(string codeUnitName, string methodName, params IEnumerable<CodeUnitParameter> parameters)
        => new(codeUnitName, methodName, parameters);
}

public record CodeUnitParameter(string ParameterName, string ParameterValue);