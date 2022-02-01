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
        /// Gets or sets the ConnectionMode used in the CosmosClient instances.
        /// </summary>
        /// <remarks>Default is Gateway mode.</remarks>
        public ConnectionMode? ConnectionMode { get; set; }

        /// <summary>
        /// Gets or sets a string to be included in the User Agent for all operations by Cosmos DB bindings and triggers.
        /// </summary>
        public string UserAgentSuffix { get; set; }

        public string Format()
        {
            StringWriter sw = new StringWriter();
            using (JsonTextWriter writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented })
            {
                writer.WriteStartObject();

                writer.WritePropertyName(nameof(this.ConnectionMode));
                writer.WriteValue(this.ConnectionMode);

                if (!string.IsNullOrEmpty(UserAgentSuffix))
                {
                    writer.WritePropertyName(nameof(this.UserAgentSuffix));
                    writer.WriteValue(this.UserAgentSuffix);
                }

                writer.WriteEndObject();
            }

            return sw.ToString();
        }
    }
}