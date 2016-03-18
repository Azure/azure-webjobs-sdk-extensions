using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    internal interface IBindingProvider2
    {
        Task<IBinding> BindDirect(BindingProviderContext context);
    }
}