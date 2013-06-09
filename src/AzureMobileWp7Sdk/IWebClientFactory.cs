using System.Net;

namespace AzuraMobileSdk
{
    public interface IWebClientFactory
    {
        WebClient GetClient();
    }
}
