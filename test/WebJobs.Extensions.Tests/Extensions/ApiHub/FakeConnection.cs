// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.ApiHub;
using Microsoft.Azure.ApiHub.Table.Internal;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.ApiHub
{
    internal class FakeConnection : Connection
    {
        public FakeConnection(FakeTabularConnectorAdapter tableAdapter)
        {
            TableAdapter = tableAdapter;
        }

        private FakeTabularConnectorAdapter TableAdapter { get; set; }

        public override ITableClient CreateTableClient()
        {
            return new TableClient(TableAdapter);
        }
    }
}
