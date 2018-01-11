// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Table
{
    internal class TableAsyncCollector<T> : IAsyncCollector<T>
        where T : class
    {
        public TableAsyncCollector(ITable<T> table)
        {
            Table = table;
        }

        private ITable<T> Table { get; set; }

        public async Task AddAsync(T item, CancellationToken cancellationToken = default(CancellationToken))
        {
            await Table.CreateEntityAsync(item);
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Batching is not supported.
            return Task.FromResult(0);
        }
    }
}
