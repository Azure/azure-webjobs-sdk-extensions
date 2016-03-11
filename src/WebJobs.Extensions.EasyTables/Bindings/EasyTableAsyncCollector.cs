// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.EasyTables
{
    internal class EasyTableAsyncCollector<T> : IAsyncCollector<T>
    {
        private EasyTableContext _context;

        public EasyTableAsyncCollector(EasyTableContext context)
        {
            _context = context;
        }

        public async Task AddAsync(T item, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (item is JObject)
            {
                IMobileServiceTable table = _context.Client.GetTable(_context.ResolvedTableName);
                await table.InsertAsync(item as JObject);
            }
            else
            {
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