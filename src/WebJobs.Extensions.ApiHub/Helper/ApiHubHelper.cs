using Microsoft.Azure.ApiHub.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    public static class ApiHubHelper
    {
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
