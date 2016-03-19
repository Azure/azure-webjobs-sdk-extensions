// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using ExtensionsSample.Models;
using Microsoft.Azure.WebJobs;

namespace ExtensionsSample.Samples
{
    // To use the DocumentDB samples:
    // 1. Create a new DocumentDB account
    // 2. Add the DocumentDB Connection String to the 'AzureWebJobsDocumentDBConnectionString' App Setting in app.config    
    // 3. Add typeof(DocumentDBSamples) to the SamplesTypeLocator in Program.cs
    public static class DocumentDBSamples
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
            [DocumentDB("ItemDb", "ItemCollection", CreateIfNotExists = true)] out Item newItem)
        {
            newItem = new Item()
            {
                Id = Guid.NewGuid().ToString(),
                Text = new Random().Next().ToString()
            };
        }
    }
}
