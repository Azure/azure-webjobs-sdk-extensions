using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    public static class ApiHubJobHostConfigurationExtensions
    {
        public static void UseApiHub(this JobHostConfiguration config, ApiHubConfiguration saas)
        {
            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<IExtensionConfigProvider>(saas);
        }
    }
}
