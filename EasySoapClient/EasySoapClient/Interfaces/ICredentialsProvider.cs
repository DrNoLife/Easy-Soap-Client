namespace EasySoapClient.Interfaces;

public interface ICredentialsProvider
{
    string GenerateBase64Credentials();
}
