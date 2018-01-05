// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub.Management;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Helper file to work with APIHub
    /// </summary>
    public static partial class ApiHubJobHostConfigurationExtensions
    {
        /// <summary>
        /// Obtain the connection string from Azure App Service.
        /// </summary>
        /// <param name="apiName">Name of the API.</param>
        /// <param name="subscriptionId">Azure subscription Id</param>
        /// <param name="location">Azure location to be used.</param>
        /// <param name="azureAdToken">Azure AD token to be used.</param>
        /// <returns>Connection string to be saved in the app setting and used for runtime calls</returns>
        public static async Task<string> GetApiHubProviderConnectionStringAsync(string apiName, string subscriptionId, string location, string azureAdToken)
        {
            var hub = new ApiHubClient(subscriptionId, location, azureAdToken);
            var connections = await hub.GetConnectionsAsync(apiName);
            var connectionKey = await hub.GetConnectionKeyAsync(connections.First());
            var connectionString = hub.GetConnectionString(connectionKey.RuntimeUri, "Key", connectionKey.Key);
            return connectionString;
        }
    }
}
