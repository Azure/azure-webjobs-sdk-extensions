// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    public class SerializerSettings
    {
        /// <summary>
        /// Gets or sets a string to be included in the User Agent for all operations by Cosmos DB bindings and triggers.
        /// </summary>
        public DateParseHandling? DateParseHandling { get; set; }
    }
}
