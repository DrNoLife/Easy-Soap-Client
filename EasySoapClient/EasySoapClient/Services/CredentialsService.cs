using EasySoapClient.Interfaces;
using EasySoapClient.Models;
using Microsoft.Extensions.Options;
using System.Text;

namespace EasySoapClient.Services;

public class CredentialsService : ICredentialsProvider
{
    private readonly string _username;
    private readonly string _password;

    public CredentialsService(IOptions<EasySoapClientOptions> options)
    {
        _username = options.Value.Username;
        _password = options.Value.Password;
    }

    public string Username => _username;
    public string Password => _password;

    public string GenerateBase64Credentials()
        => Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Username}:{Password}"));
}

