// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    public class SerializerSettings
    {
        /// <summary>
        /// Gets or sets the DateParseHandling to be used in CosmosDB serializer settings.
        /// </summary>
        public DateParseHandling? DateParseHandling { get; set; }
    }
}
