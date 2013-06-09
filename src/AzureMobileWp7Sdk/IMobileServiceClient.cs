using System.Threading.Tasks;

namespace AzuraMobileSdk
{
    public interface IMobileServiceClient
    {
        Task<string> Get(string relativeUrl);
        Task<string> Post(string relativeUrl, object payload);
        Task Delete(string relativeUrl);
        Task<string> Patch(string relativeUrl, object payload);
    }
}