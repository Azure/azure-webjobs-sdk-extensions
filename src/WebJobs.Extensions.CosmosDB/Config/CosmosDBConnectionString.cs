// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Data.Common;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Config
{
    /// <summary>
    /// A strongly-typed CosmosDB connection string. DocumentClient does not currently
    /// support connection strings so we are using the base DbConnectionStringBuilder to 
    /// perform the parsing for us. When it is handled by DocumentClient itself, we'll remove
    /// this class.
    /// </summary>
    internal class CosmosDBConnectionString
    {
        public CosmosDBConnectionString(string connectionString)
        {
            // Use this generic builder to parse the connection string
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            object key = null;
            if (builder.TryGetValue("AccountKey", out key))
            {
                AuthKey = key.ToString();
            }

            object uri;
            if (builder.TryGetValue("AccountEndpoint", out uri))
            {
                ServiceEndpoint = new Uri(uri.ToString());
            }
        }

        public Uri ServiceEndpoint { get; set; }
        public string AuthKey { get; set; }
    }
}
