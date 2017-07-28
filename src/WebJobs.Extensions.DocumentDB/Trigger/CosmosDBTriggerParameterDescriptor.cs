// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs.Host.Protocols;

    /// <summary>
    /// Trigger parameter descriptor for [CosmosDBTrigger]
    /// </summary>
    internal class CosmosDBTriggerParameterDescriptor : TriggerParameterDescriptor
    {
        /// <summary>
        /// Name of the collection being monitored
        /// </summary>
        public string CollectionName { get; set; }

        public override string GetTriggerReason(IDictionary<string, string> arguments)
        {
            return string.Format(CosmosDBTriggerConstants.TriggerDescription, this.CollectionName, DateTime.UtcNow.ToString("o"));
        }
    }
}
