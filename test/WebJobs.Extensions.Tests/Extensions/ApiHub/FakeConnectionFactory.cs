// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.ApiHub
{
    internal class FakeConnectionFactory : ConnectionFactory
    {
        public FakeConnectionFactory(FakeTabularConnectorAdapter tableAdapter) : base()
        {
            TableAdapter = tableAdapter;
        }

        private FakeTabularConnectorAdapter TableAdapter { get; set; }

        public override Connection CreateConnection(string key)
        {
            return new FakeConnection(TableAdapter);
        }
    }
}
