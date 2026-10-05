using Microsoft.Extensions.Options;

namespace EasySoapClient.Models;

internal sealed class EasySoapClientOptionsValidator : IValidateOptions<EasySoapClientOptions>
{
    public ValidateOptionsResult Validate(string? name, EasySoapClientOptions options)
    {
        string client = String.IsNullOrEmpty(name) ? "EasySoapClient" : $"EasySoapClient '{name}'";
        List<string> failures = [];

        if (String.IsNullOrWhiteSpace(options.BaseUri))
        {
            failures.Add($"{client}: BaseUri is required.");
        }
        else if (!Uri.TryCreate(options.BaseUri, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add($"{client}: BaseUri '{options.BaseUri}' is not an absolute http(s) URL.");
        }
        else if (!String.IsNullOrEmpty(uri.Fragment))
        {
            failures.Add($"{client}: BaseUri '{options.BaseUri}' must not contain a fragment ('#...').");
        }

        if (!Enum.IsDefined(options.AuthenticationMode))
        {
            failures.Add($"{client}: AuthenticationMode '{options.AuthenticationMode}' is not valid.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
