using System.Text;
using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using Microsoft.Extensions.Options;

namespace EasySoapClient.Services;

/// <summary>
/// Default <see cref="ICredentialsProvider"/>: reads the named options on every call, so configuration reloads apply.
/// </summary>
internal sealed class CredentialsService(IOptionsMonitor<EasySoapClientOptions> optionsMonitor, string optionsName) : ICredentialsProvider
{
    private readonly IOptionsMonitor<EasySoapClientOptions> _optionsMonitor = optionsMonitor;
    private readonly string _optionsName = optionsName;

    public string GenerateBase64Credentials()
    {
        EasySoapClientOptions options = _optionsMonitor.Get(_optionsName);

        // Checked here rather than in options validation, so a custom ICredentialsProvider works without a Username.
        if (String.IsNullOrEmpty(options.Username))
        {
            string client = String.IsNullOrEmpty(_optionsName) ? "EasySoapClient" : $"EasySoapClient '{_optionsName}'";
            throw new InvalidOperationException($"{client}: Username is required for Basic authentication (or register a custom {nameof(ICredentialsProvider)}).");
        }

        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
    }
}
