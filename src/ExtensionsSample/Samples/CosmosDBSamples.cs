// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExtensionsSample.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json.Linq;

namespace ExtensionsSample
{
    // To use the CosmosDB samples:
    // 1. Create a new CosmosDB account
    // 2. Add the CosmosDB Connection String to the "cosmosDB" property (under "connectionStrings") in appsettings.json    
    // 3. Add typeof(CosmosDBSamples) to the SamplesTypeLocator in Program.cs
    public static class CosmosDBSamples
    {
        // POCO Output binding
        //   The binding inserts the newly created item into the specified Collection when the
        //   method successfully exits.
        //   If the database or the collection do not exist, they are automatically created.
        //
        // Other supported types:
        //   out T[]
        //   IAsyncCollector<T>
        //   ICollector<T>        
        public static void InsertDocument(
            [TimerTrigger("00:01")] TimerInfo timer,
            [CosmosDB("ItemDb", "ItemCollection", CreateIfNotExists = true)] out ItemDoc newItem)
        {
            newItem = new ItemDoc()
            {
                Text = new Random().Next().ToString(),
                IsCompleted = new Random().Next() % 2 == 0
            };
        }

        // Document input binding
        //   This binding requires the 'Id' property to be specified. The binding uses
        //   that id to perform a lookup against the specified collection. The resulting object is supplied to the
        //   function parameter (or null if not found).
        //   Any changes made to the item are updated when the function exits successfully. If there are 
        //   no changes, nothing is sent.
        //
        // This example uses the binding template "{QueueTrigger}" to specify that the Id should come from
        // the string value of the queued item.
        public static void ReadDocument(
            [QueueTrigger("samples-cosmosdb-csharp")] string input,
            [CosmosDB("ItemDb", "ItemCollection", Id = "{QueueTrigger}")] JObject item)
        {
            item["text"] = "Text changed!";
        }

        // Document query input binding
        //   This binding requires the 'SqlQuery' property to be specified. The binding uses
        //   that string to issue a query against the specified collection. The resulting collection is passed to the 
        //   function parameter.
        //
        // This example uses the binding template "{IsCompleted}" to specify that the value of 'c.isCompleted' should come
        // from the IsCompleted proparty of the queued JSON message.
        public static void QueryDocument(
            [QueueTrigger("samples-cosmosdb-csharp-query")] CustomQueueInput input,
            [CosmosDB("ItemDb", "ItemCollection", SqlQuery = "SELECT c.id, c.text FROM c where c.isCompleted = {IsCompleted}")] IEnumerable<string> items,
            TraceWriter log)
        {
            foreach (var doc in items)
            {
                log.Info($"Text: {doc}");
            }
        }

        // DocumentClient input binding
        //   The binding supplies a CosmosClient directly.
        [Disable]
        public static async Task CosmosClient(
            [TimerTrigger("00:01", RunOnStartup = true)] TimerInfo timer,
            [CosmosDB] CosmosClient client,
            TraceWriter log)
        {
            var iterator = client.GetContainer("ItemDb", "ItemCollection").GetItemQueryIterator<dynamic>("SELECT * FROM c");

            while (iterator.HasMoreResults)
            {
                var documents = await iterator.ReadNextAsync();
                foreach (dynamic d in documents)
                {
                    log.Info(d.id);
                }
            }
        }
    }
}
