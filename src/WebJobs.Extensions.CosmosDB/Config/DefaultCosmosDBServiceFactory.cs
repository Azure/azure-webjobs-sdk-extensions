// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class DefaultCosmosDBServiceFactory : ICosmosDBServiceFactory
    {
        private readonly IConfiguration _configuration;
        private readonly AzureComponentFactory _componentFactory;

        public DefaultCosmosDBServiceFactory(
            IConfiguration configuration,
            AzureComponentFactory componentFactory)
        {
            this._configuration = configuration;
            this._componentFactory = componentFactory;
        }

        public CosmosClient CreateService(string connectionName, CosmosClientOptions cosmosClientOptions)
        {
            CosmosConnectionInformation cosmosConnectionInformation = this.ResolveConnectionInformation(connectionName);
            if (cosmosConnectionInformation.UsesConnectionString)
            {
                // Connection string based auth
                return new CosmosClient(cosmosConnectionInformation.ConnectionString, cosmosClientOptions);
            }

            // AAD auth
            return new CosmosClient(cosmosConnectionInformation.AccountEndpoint, cosmosConnectionInformation.Credential, cosmosClientOptions);
        }

        private CosmosConnectionInformation ResolveConnectionInformation(string connection)
        {
            var connectionSetting = connection ?? Constants.DefaultConnectionStringName;
            IConfigurationSection connectionSection = GetWebJobsConnectionStringSectionCosmos(this._configuration, connectionSetting);
            if (!connectionSection.Exists())
            {
                // Not found
                throw new InvalidOperationException($"Cosmos DB connection configuration '{connectionSetting}' does not exist. " +
                                                    $"Make sure that it is a defined App Setting.");
            }

            if (!string.IsNullOrWhiteSpace(connectionSection.Value))
            {
                return new CosmosConnectionInformation(connectionSection.Value);
            }
            else
            {
                string accountEndpoint = connectionSection["accountEndpoint"];
                if (string.IsNullOrWhiteSpace(accountEndpoint))
                {
                    // Not found
                    throw new InvalidOperationException($"Connection should have an 'accountEndpoint' property or be a " +
                        $"string representing a connection string.");
                }

                TokenCredential credential = _componentFactory.CreateTokenCredential(connectionSection);
                return new CosmosConnectionInformation(accountEndpoint, credential);
            }
        }

        public static IConfigurationSection GetWebJobsConnectionStringSectionCosmos(IConfiguration configuration, string connectionStringName)
        {
            // first try a direct unprefixed lookup
            IConfigurationSection section = WebJobsConfigurationExtensions.GetConnectionStringOrSetting(configuration, connectionStringName);

            if (!section.Exists())
            {
                // next try prefixing
                string prefixedConnectionStringName = WebJobsConfigurationExtensions.GetPrefixedConnectionStringName(connectionStringName);
                section = WebJobsConfigurationExtensions.GetConnectionStringOrSetting(configuration, prefixedConnectionStringName);
            }

            return section;
        }

        private class CosmosConnectionInformation
        {
            public CosmosConnectionInformation(string connectionString)
            {
                this.ConnectionString = connectionString;
                this.UsesConnectionString = true;
            }

            public CosmosConnectionInformation(string accountEndpoint, TokenCredential tokenCredential)
            {
                this.AccountEndpoint = accountEndpoint;
                this.Credential = tokenCredential;
                this.UsesConnectionString = false;
            }

            public bool UsesConnectionString { get; }

            public string ConnectionString { get; }

            public string AccountEndpoint { get; }

            public TokenCredential Credential { get; }
        }
    }
}
