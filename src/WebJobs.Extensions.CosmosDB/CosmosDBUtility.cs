// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal static class CosmosDBUtility
    {
        internal static bool TryGetCosmosException(Exception originalEx, out CosmosException cosmosException)
        {
            cosmosException = null;
            if (originalEx is CosmosException originalCosmosException)
            {
                cosmosException = originalCosmosException;
                return true;
            }

            AggregateException ae = originalEx as AggregateException;
            if (ae == null)
            {
                return false;
            }

            if (ae.InnerException is CosmosException nestedCosmosException)
            {
                cosmosException = nestedCosmosException;
                return true;
            }

            return false;
        }

        internal static async Task CreateDatabaseAndCollectionIfNotExistAsync(CosmosDBContext context)
        {
            await CreateDatabaseAndCollectionIfNotExistAsync(context.Service, context.ResolvedAttribute.DatabaseName, context.ResolvedAttribute.ContainerName,
                context.ResolvedAttribute.PartitionKey, context.ResolvedAttribute.ContainerThroughput);
        }

        internal static async Task CreateDatabaseAndCollectionIfNotExistAsync(CosmosClient service, string databaseName, string containerName, string partitionKey, int? throughput)
        {
            await service.CreateDatabaseIfNotExistsAsync(databaseName);

            int? desiredThroughput = null;
            if (throughput.HasValue && throughput.Value > 0)
            {
                desiredThroughput = throughput;
            }

            Database database = service.GetDatabase(databaseName);

            try
            {
                await database.GetContainer(containerName).ReadContainerAsync();
            }
            catch (CosmosException cosmosException) when (cosmosException.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await database.CreateContainerAsync(containerName, partitionKey, desiredThroughput);
            }
        }

        internal static CosmosClientOptions BuildClientOptions(ConnectionMode? connectionMode, CosmosSerializer serializer, string preferredLocations, string userAgent)
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions();
            if (connectionMode.HasValue)
            {
                cosmosClientOptions.ConnectionMode = connectionMode.Value;
            }
            else
            {
                // Default is Gateway to avoid issues with Functions and consumption plan
                cosmosClientOptions.ConnectionMode = ConnectionMode.Gateway;
            }

            if (!string.IsNullOrEmpty(preferredLocations))
            {
                cosmosClientOptions.ApplicationPreferredRegions = ParsePreferredLocations(preferredLocations);
            }

            if (!string.IsNullOrEmpty(userAgent))
            {
                cosmosClientOptions.ApplicationName = userAgent;
            }

            if (serializer != null)
            {
                cosmosClientOptions.Serializer = serializer;
            }

            return cosmosClientOptions;
        }

        internal static IReadOnlyList<string> ParsePreferredLocations(string preferredRegions)
        {
            if (string.IsNullOrEmpty(preferredRegions))
            {
                return Enumerable.Empty<string>().ToList();
            }

            return preferredRegions
                .Split(',')
                .Select((region) => region.Trim())
                .Where((region) => !string.IsNullOrEmpty(region))
                .ToList();
        }
    }
}
