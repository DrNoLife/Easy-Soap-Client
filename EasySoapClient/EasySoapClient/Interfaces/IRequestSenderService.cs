using EasySoapClient.Contracts.CodeUnit;
using EasySoapClient.Enums;

namespace EasySoapClient.Interfaces;

public interface IRequestSenderService
{
    Task<string> SendWebServiceSoapRequestAsync(
        CallMethod soapMethod, 
        string soapEnvelope, 
        IWebServiceElement instance, 
        CancellationToken cancellationToken = default);

    Task<string> SendCodeUnitSoapRequestAsync(
        CodeUnitRequest request, 
        string soapEnvelope,
        CancellationToken cancellationToken = default);
}
