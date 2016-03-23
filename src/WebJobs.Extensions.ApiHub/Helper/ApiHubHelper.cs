using Microsoft.Azure.ApiHub.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    /// <summary>
    /// Helper file to work with APIHub
    /// </summary>
    public static class ApiHubHelper
    {
        /// <summary>
        /// Obtain the connection string from Azure App Service.
        /// For example see https://github.com/Azure/azure-apihub-sdk/blob/master/samples/ManagedApis.ps1
        /// </summary>
        /// <param name="apiName">Name of the API. Get a name from <example>armclient get "/subscriptions/83e6374a-dfa5-428b-82ef-eab6c6bdd383/providers/Microsoft.Web/locations/brazilsouth/managedApis?api-version=2015-08-01-preview" -verbose</example></param>
        /// <param name="subscriptionId">Azure subscription Id</param>
        /// <param name="location">Azure location to be used. See <example>armclient get "/providers/Microsoft.Web/?api-version=2015-11-01" -verbose </example>. Note strip empty spaces</param>
        /// <param name="aadToken">Azure ADD token to be used. <example>armclient login | armclient token</example></param>
        /// <returns>Connection string to be saved in the app setting and used for runtime calls</returns>
        public async static Task<string> GetApiHubProviderConnectionStringAsync(string apiName, string subscriptionId, string location, string aadToken)
        {
            var hub = new ApiHubClient(subscriptionId, location, aadToken);
            var connections = await hub.GetConnectionsAsync(apiName);
            var connectionKey = await hub.GetConnectionKeyAsync(connections.First());
            var connectionString = hub.GetConnectionString(connectionKey.RuntimeUri, "Key", connectionKey.Key);
            return connectionString;
        }

    }
}
