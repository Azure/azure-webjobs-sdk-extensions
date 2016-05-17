// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.ApiHub.Sdk;
using Microsoft.Azure.ApiHub.Sdk.Table;
using Microsoft.Azure.ApiHub.Sdk.Table.Internal;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.ApiHub
{
    internal class FakeConnection : Connection
    {
        public FakeConnection(FakeTabularConnectorAdapter tableAdapter)
        {
            TableAdapter = tableAdapter;
        }

        private FakeTabularConnectorAdapter TableAdapter { get; }

        public override ITableClient CreateTableClient()
        {
            return new TableClient(TableAdapter);
        }
    }
}
