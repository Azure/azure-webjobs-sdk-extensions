// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;
using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    /// <summary>
    /// Trigger parameter descriptor for [CosmosDBTrigger]
    /// </summary>
    internal class CosmosDBCassandraTriggerParameterDescriptor : TriggerParameterDescriptor
    {
        /// <summary>
        /// Name of the keyspace being monitored
        /// </summary>
        public string KeyspaceName { get; set; }

        /// <summary>
        /// Name of the table being monitored
        /// </summary>
        public string TableName { get; set; }

        public override string GetTriggerReason(IDictionary<string, string> arguments)
        {
            return string.Format(CosmosDBCassandraTriggerConstants.TriggerDescription, this.KeyspaceName, this.TableName, DateTime.UtcNow.ToString("o"));
        }
    }
}
