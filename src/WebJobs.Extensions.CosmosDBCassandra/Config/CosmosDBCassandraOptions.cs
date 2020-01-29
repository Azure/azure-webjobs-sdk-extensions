// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

//using Microsoft.Azure.Documents.ChangeFeedProcessor;
//using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json;
using System.IO;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    public class CosmosDBCassandraOptions : IOptionsFormatter
    {
        /// <summary>
        /// Gets or sets the CosmosDB connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the ConnectionMode used in the DocumentClient instances.
        /// </summary>
        //public ConnectionMode? ConnectionMode { get; set; }

        /// <summary>
        /// Gets or sets the Protocol used in the DocumentClient instances.
        /// </summary>
        //public Protocol? Protocol { get; set; }

        /// <summary>
        /// Gets or sets the lease options for the DocumentDB Trigger. 
        /// </summary>
        //public ChangeFeedHostOptions LeaseOptions { get; set; } = new ChangeFeedHostOptions();

        public string Format()
        {
            StringWriter sw = new StringWriter();
            using (JsonTextWriter writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented })
            {
                writer.WriteStartObject();

                //writer.WritePropertyName(nameof(ConnectionMode));
                //writer.WriteValue(ConnectionMode);

                //writer.WritePropertyName(nameof(Protocol));
                //writer.WriteValue(Protocol);

                writer.WritePropertyName("dummy options");

                writer.WriteStartObject();

                //writer.WritePropertyName(nameof(ChangeFeedHostOptions.CheckpointFrequency));
                //writer.WriteStartObject();

                //writer.WritePropertyName(nameof(CheckpointFrequency.ExplicitCheckpoint));
                //writer.WriteValue(LeaseOptions.CheckpointFrequency.ExplicitCheckpoint);

                //writer.WritePropertyName(nameof(CheckpointFrequency.ProcessedDocumentCount));
                //writer.WriteValue(LeaseOptions.CheckpointFrequency.ProcessedDocumentCount);

                //writer.WritePropertyName(nameof(CheckpointFrequency.TimeInterval));
                //writer.WriteValue(LeaseOptions.CheckpointFrequency.TimeInterval);

                writer.WriteEndObject();

                //writer.WritePropertyName(nameof(ChangeFeedHostOptions.FeedPollDelay));
                //writer.WriteValue(LeaseOptions.FeedPollDelay);

                //writer.WritePropertyName(nameof(ChangeFeedHostOptions.IsAutoCheckpointEnabled));
                //writer.WriteValue(LeaseOptions.IsAutoCheckpointEnabled);

                //writer.WritePropertyName(nameof(ChangeFeedHostOptions.LeaseAcquireInterval));
                //writer.WriteValue(LeaseOptions.LeaseAcquireInterval);

                //writer.WritePropertyName(nameof(ChangeFeedHostOptions.LeaseExpirationInterval));
                //writer.WriteValue(LeaseOptions.LeaseExpirationInterval);

                //writer.WritePropertyName(nameof(ChangeFeedHostOptions.LeasePrefix));
                //writer.WriteValue(LeaseOptions.LeasePrefix);

                //writer.WritePropertyName(nameof(ChangeFeedHostOptions.LeaseRenewInterval));
                //writer.WriteValue(LeaseOptions.LeaseRenewInterval);

                //writer.WriteEndObject();

                //writer.WriteEndObject();
            }

            return sw.ToString();
        }
    }
}