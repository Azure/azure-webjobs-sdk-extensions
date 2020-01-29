// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Hosting;

[assembly: WebJobsStartup(typeof(CosmosDBCassandraWebJobsStartup))]

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    public class CosmosDBCassandraWebJobsStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            
            builder.AddCosmosDBCassandra();
        }
    }
}
