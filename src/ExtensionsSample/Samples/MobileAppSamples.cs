// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExtensionsSample.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.MobileServices;

namespace ExtensionsSample
{
    // To use the MobileAppSamples:
    // 1. Create a new Mobile App
    // 2. Create a new 'Item' Easy Table in the Mobile App
    // 3. Add the Mobile App URI to a 'AzureWebJobsMobileAppUri' App Setting in app.config    
    // 4. Add typeof(MobileAppSamples) to the SamplesTypeLocator in Program.cs
    public static class MobileAppSamples
    {
        // POCO output binding 
        //   The binding inserts the newly created item into the table when the 
        //   method successfully exits.         
        public static void InsertItem(
            [TimerTrigger("00:01")] TimerInfo timer,
            [MobileTable] out Item newItem)
        {
            newItem = new Item()
            {
                Id = Guid.NewGuid().ToString(),
                Text = new Random().Next().ToString()
            };
        }

        // Anonymous output binding 
        //   The binding inserts the newly created item into the table when the 
        //   method successfully exits.
        public static void InsertItem_TableItem(
            [TimerTrigger("00:01")] TimerInfo timer,
            [MobileTable(TableName = "Item")] out object newItem)
        {
            newItem = new
            {
                id = Guid.NewGuid().ToString(),
                text = new Random().Next().ToString()
            };
        }

        // Query input binding 
        //   The binding creates a strongly-typed query against the Item table. 
        //   The binding does not do anything with the results when the function exits.  
        //  This example takes the results of the query and puts them in a queue for further 
        //  processing. 
        public static async Task EnqueueItemToProcess(
            [TimerTrigger("00:01")] TimerInfo timer,
            [MobileTable] IMobileServiceTableQuery<Item> itemQuery,
            [Queue("ToProcess")] IAsyncCollector<string> queueItems)
        {
            IEnumerable<string> itemsToProcess = await itemQuery
                .Where(i => !i.IsProcessed)
                .Select(i => i.Id)
                .ToListAsync();

            foreach (string itemId in itemsToProcess)
            {
                await queueItems.AddAsync(itemId);
            }
        }

        // POCO input/output binding 
        //   This binding requires the 'Id' property to be specified. The binding uses 
        //   that id to perform a lookup against the table. The resulting object is supplied to the 
        //   function parameter (or null if not found). 
        //   Any changes made to the item are updated when the function exits successfully. If there are  
        //   no changes, nothing is sent.         
        // This example uses the binding template "{QueueTrigger}" to specify that the Id should come from 
        // the string value of the queued item.
        public static void DequeueAndProcess(
            [QueueTrigger("ToProcess")] string itemId,
            [MobileTable(Id = "{QueueTrigger}")] Item itemToProcess)
        {
            itemToProcess.IsProcessed = true;
            itemToProcess.ProcessedAt = DateTimeOffset.Now;
        }

        // Table input binding 
        //   This binding supplies a strongly-typed IMobileServiceTable<T> to the function. This allows 
        //   for queries, inserts, updates, and deletes to all be made against the table. 
        //   See the 'Input bindings' section below for more info. 
        public static async Task DeleteProcessedItems(
            [TimerTrigger("00:05")] TimerInfo timerInfo,
            [MobileTable] IMobileServiceTable<Item> table)
        {
            IEnumerable<Item> processedItems = await table.CreateQuery()
                .Where(i => i.IsProcessed && i.ProcessedAt < DateTime.Now.AddMinutes(-5))
                .ToListAsync();

            foreach (Item i in processedItems)
            {
                await table.DeleteAsync(i);
            }
        }
    }
}
