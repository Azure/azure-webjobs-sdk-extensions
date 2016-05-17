// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Table
{
    internal class TableConfigContext
    {
        public TableConfigContext(
            ConnectionFactory connectionFactory,
            INameResolver nameResolver)
        {
            ConnectionFactory = connectionFactory;
            NameResolver = nameResolver;
        }

        public ConnectionFactory ConnectionFactory { get; }

        public INameResolver NameResolver { get; }
    }
}
