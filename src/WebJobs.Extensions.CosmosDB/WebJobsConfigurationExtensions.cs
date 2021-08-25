// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal static class WebJobsConfigurationExtensions
    {
        private const string WebJobsConfigurationSectionName = "AzureWebJobs";

        public static IConfigurationSection GetWebJobsConnectionStringSection(this IConfiguration configuration, string connectionStringName)
        {
            // first try prefixing
            string prefixedConnectionStringName = GetPrefixedConnectionStringName(connectionStringName);
            IConfigurationSection section = GetConnectionStringOrSetting(configuration, prefixedConnectionStringName);

            if (!section.Exists())
            {
                // next try a direct unprefixed lookup
                section = GetConnectionStringOrSetting(configuration, connectionStringName);
            }

            return section;
        }

        public static string GetPrefixedConnectionStringName(string connectionStringName)
        {
            return WebJobsConfigurationSectionName + connectionStringName;
        }

        /// <summary>
        /// Looks for a connection string by first checking the ConfigurationStrings section, and then the root.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="connectionName">The connection string key.</param>
        /// <returns></returns>
        public static IConfigurationSection GetConnectionStringOrSetting(this IConfiguration configuration, string connectionName)
        {
            if (configuration.GetSection("ConnectionStrings").Exists())
            {
                return configuration.GetSection("ConnectionStrings").GetSection(connectionName);
            }

            return configuration.GetSection(connectionName);
        }
    }
}