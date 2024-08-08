using EasySoapClient.Interfaces;
using System.Text;

namespace EasySoapClient.Models;

public struct Credentials(string username, string password) : ICredentialsProvider
{
    public string Username => username;
    public string Password => password;

    public string GenerateBase64Credentials()
        => Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Username}:{Password}"));
}