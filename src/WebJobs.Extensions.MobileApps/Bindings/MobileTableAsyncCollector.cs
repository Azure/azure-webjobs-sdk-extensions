// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    internal class MobileTableAsyncCollector<T> : IAsyncCollector<T>
    {
        private MobileTableContext _context;

        public MobileTableAsyncCollector(MobileTableContext context)
        {
            _context = context;
        }

        public async Task AddAsync(T item, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (typeof(T) == typeof(JObject) || typeof(T) == typeof(object))
            {
                if (string.IsNullOrEmpty(_context.ResolvedTableName))
                {
                    throw new InvalidOperationException("The table name must be specified.");
                }

                // If the item is an object, that is inferred to mean it is an anonymous type and we convert it directly
                // to a JObject. Items of type object are not directly usable with the Mobile Service client as there is
                // no 'Id' property on Object. This adds some useful functionality from scripting where you don't need to
                // define models or add references to JSON.NET in order to add data to table.
                JObject convertedItem = JObject.FromObject(item);
                IMobileServiceTable table = _context.Client.GetTable(_context.ResolvedTableName);
                await table.InsertAsync(convertedItem);
            }
            else
            {
                // If TableName is specified, add it to the internal table cache. Now items of this type
                // will operate on the specified TableName.
                if (!string.IsNullOrEmpty(_context.ResolvedTableName))
                {
                    _context.Client.AddToTableNameCache(item.GetType(), _context.ResolvedTableName);
                }

                IMobileServiceTable<T> table = _context.Client.GetTable<T>();
                await table.InsertAsync(item);
            }
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Mobile Services does not support batching.
            return Task.FromResult(0);
        }
    }
}