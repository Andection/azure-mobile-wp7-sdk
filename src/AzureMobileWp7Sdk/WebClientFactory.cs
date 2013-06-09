using System.Net;

namespace AzuraMobileSdk
{
    public class WebClientFactory : IWebClientFactory
    {
        public WebClient GetClient()
        {
            return new WebClient();
        }

        public static IWebClientFactory Get()
        {
            return new WebClientFactory();
        }
    }
}