// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for CosmosDB integration.
    /// </summary>
    public static class CosmosDBJobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of the CosmosDB extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="cosmosDBConfig">The <see cref="CosmosDBConfiguration"/> to use.</param>
        public static void UseCosmosDB(this JobHostConfiguration config, CosmosDBConfiguration cosmosDBConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (cosmosDBConfig == null)
            {
                cosmosDBConfig = new CosmosDBConfiguration();
            }

            config.RegisterExtensionConfigProvider(cosmosDBConfig);
        }
    }
}
