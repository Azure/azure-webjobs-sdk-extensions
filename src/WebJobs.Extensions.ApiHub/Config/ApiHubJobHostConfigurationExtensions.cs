using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    /// <summary>
    /// Helper extensions for ApiHub configuration
    /// </summary>
    public static class ApiHubJobHostConfigurationExtensions
    {
        /// <summary>
        /// Add ApiHub ocnfiguration to <see cref="JobHostConfiguration"/>
        /// </summary>
        /// <param name="config">curent <see cref="JobHostConfiguration"/></param>
        /// <param name="saasConfiguration">Instance of <see cref="ApiHubConfiguration"/></param>
        public static void UseApiHub(this JobHostConfiguration config, ApiHubConfiguration saasConfiguration)
        {
            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<IExtensionConfigProvider>(saasConfiguration);
        }
    }
}
