// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Hosting;

[assembly: WebJobsStartup(typeof(CosmosDBWebJobsStartup))]

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    public class CosmosDBWebJobsStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddCosmosDB();
        }

        /*
        public void Configure(WebJobsBuilderContext context, IWebJobsBuilder builder)
        {
            builder.AddCosmosDB(options =>
            {
                /*
                // Configure your CosmosDB options here
                options.ConnectionMode = context.Configuration.GetSection("CosmosDB").ToString();
                options.DatabaseName = "your-database-name";
                // Other properties of CosmosDBOptions
                
            }
        }
        */
    }
}
