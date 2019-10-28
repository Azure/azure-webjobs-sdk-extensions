// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    public class CosmosDBOptions : IOptionsFormatter
    {
        /// <summary>
        /// Gets or sets the CosmosDB connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the ConnectionMode used in the CosmosClient instances.
        /// </summary>
        /// <remarks>Default is Gateway mode.</remarks>
        public ConnectionMode? ConnectionMode { get; set; }

        public string Format()
        {
            StringWriter sw = new StringWriter();
            using (JsonTextWriter writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented })
            {
                writer.WriteStartObject();

                writer.WritePropertyName(nameof(this.ConnectionMode));
                writer.WriteValue(this.ConnectionMode);

                writer.WriteEndObject();
            }

            return sw.ToString();
        }
    }
}