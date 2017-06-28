// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json.Linq;

namespace ExtensionsSample.Samples
{
    // To use the DocumentDB samples:
    // 1. Create a new DocumentDB account
    // 2. Add the DocumentDB Connection String to the 'AzureWebJobsDocumentDBTriggerConnectionString' App Setting in app.config    
    // 3. Create a Database called ItemDb.
    // 4. Create a Collection named ItemCollection. This will be the monitored collection.
    // 5. Create a Collection named Leases. This will be used to store auxiliary leases to support multi-observer scenarios and partitioning.
    // 6. Optionally create a Collection named ItemCollectionCopy for the ListenAndCopy scenario. And add an 'AzureWebJobsDocumentDBConnectionString' App Setting in app.config with its Connection String
    // 7. Add typeof(DocumentDBTriggerSamples) to the SamplesTypeLocator in Program.cs
    public static class CosmosDBTriggerSamples
    {
        // Sample implementation of the CosmosDBTrigger that listens for changes in a collection.
        // The trigger uses an auxiliary collection for leases for multiple partitions.
        public static void Listen(
            [CosmosDBTrigger("ItemDb", "ItemCollection", LeaseCollectionName = "Leases")] IReadOnlyList<Document> modifiedDocuments,
            TraceWriter log)
        {
            foreach (Document modifiedDocument in modifiedDocuments)
            {
                log.Info(modifiedDocument.Id);
            }
        }

        public static void ListenJArray(
            [CosmosDBTrigger("ItemDb", "ItemCollection", LeaseCollectionName = "Leases")] JArray modifiedDocuments,
            TraceWriter log)
        {
            foreach (var modifiedDocument in modifiedDocuments.Children())
            {
                log.Info(modifiedDocument["id"].ToString());
            }
        }

        // Sample implementation of the CosmosDBTrigger that listens for changes in a collection.
        // The trigger uses an auxiliary collection for leases for multiple partitions.
        // This sample will also copy modifications to another target collection.
        public static async Task ListenAndCopy(
            [CosmosDBTrigger("ItemDb", "ItemCollection", LeaseCollectionName = "Leases")] IReadOnlyList<Document> modifiedDocuments,
            [DocumentDB("ItemDb", "ItemCollectionCopy")] IAsyncCollector<Document> copyItems)
        {
            foreach (Document modifiedDocument in modifiedDocuments)
            {
                await copyItems.AddAsync(modifiedDocument);
            }
        }
    }
}
