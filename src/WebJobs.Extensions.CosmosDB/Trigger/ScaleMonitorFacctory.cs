// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    public class ScaleMonitorFacctory : IScaleMonitorFactory
    {
        private static CosmosClientOptions defaultCosmosClientOptions = new CosmosClientOptions()
        {
            // Gateway mode reduces number of connections at the expense of performance/latency
            ConnectionMode = ConnectionMode.Gateway,

            // Adding ApplicationName to stamp all requests with this value in the User Agent that can help identify traffic
            ApplicationName = "Antares-ScaleController"
        };

        public IScaleMonitor Create(ScaleMonitorContext context)
        {
            // TODO if we can provide the feature that hydrate the Attribute and configration, extension owners can share the validation logic
            CosmosClient monitorClient = null;
            CosmosClient leaseClient = null;
            if (!TryCreateCosmosClient(context["connectionStringSetting"], defaultCosmosClientOptions, out monitorClient, context.Logger) ||
                !TryCreateCosmosClient(context["leaseConnectionStringSetting"], defaultCosmosClientOptions, out leaseClient, context.Logger))
            {
                throw new ArgumentException($"Function Name: {context.FunctionName}.Unable to create CosmosClient for the trigger and / or lease collection.");
            }
            Container monitorContainer = monitorClient.GetContainer(context["databaseName"], context["collectionName"]);
            Container leaseContainer = leaseClient.GetContainer(context["leaseDatabaseName"], context["leaseCollectionName"]);
            var descriptor = new ScaleMonitorDescriptor($"ScaleController-{context.FunctionName}-CosmosDBTrigger-{monitorContainer.Database.Id}-{monitorContainer.Id}".ToLower());
            return new CosmosDBScaleMonitor(context.Logger, monitorContainer, leaseContainer, context["processName"], descriptor);
        }

        // TODO: Adding Managed Identity Support.
        private static bool TryCreateCosmosClient(string connectionString, CosmosClientOptions options, out CosmosClient cosmosClient, ILogger logger)
        {
            try
            {
                cosmosClient = new CosmosClient(connectionString, options);
                return true;
            }
            catch (UriFormatException e)
            {
                logger.LogWarning($"The lease and/or trigger collection have a malformed account endpoint URI. Exception: {e.Message}");
            }
            catch (FormatException e)
            {
                logger.LogWarning($"The lease and/or trigger collection have a malformed account key. Exception: {e.Message}");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Unable to create CosmosClient with ConnectionString in CosmosClientProvider.TryCreateCosmosClient. Exception: {0}", ex.ToString());
            }
            cosmosClient = default;
            return false;
        }
    }
}
