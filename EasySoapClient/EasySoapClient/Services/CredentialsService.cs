using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text;

namespace EasySoapClient.Services;

public class CredentialsService : ICredentialsProvider
{
    private readonly string _username;
    private readonly string _password;

    public CredentialsService(
        IOptionsMonitor<EasySoapClientOptions> optionsMonitor,
        [ServiceKey] string? serviceKey = null)
    {
        var options = serviceKey is not null
            ? optionsMonitor.Get(serviceKey) // Keyed configuration.
            : optionsMonitor.CurrentValue;   // Non-keyed (default) configuration.

        _username = options.Username ?? throw new ArgumentNullException(nameof(options.Username), "Username cannot be null.");
        _password = options.Password ?? throw new ArgumentNullException(nameof(options.Password), "Password cannot be null.");
    }

    public string Username => _username;
    public string Password => _password;

    public string GenerateBase64Credentials()
        => Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Username}:{Password}"));
}

