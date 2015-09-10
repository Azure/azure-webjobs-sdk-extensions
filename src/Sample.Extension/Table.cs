// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Sample.Extension
{
    /// <summary>
    /// Binding type demonstrating how custom binding extensions can be used to bind to
    /// arbitrary types
    /// </summary>
    public class Table<TEntity>
    {
        private readonly CloudTable _table;

        public Table(CloudTable table)
        {
            _table = table;
        }

        public void Add(TEntity entity)
        {
            // storage operations here
        }

        public void Delete(TEntity entity)
        {
            // storage operations here
        }

        internal Task FlushAsync(CancellationToken cancellationToken)
        {
            // complete and flush all storage operations
            return Task.FromResult(true);
        }
    }
}
