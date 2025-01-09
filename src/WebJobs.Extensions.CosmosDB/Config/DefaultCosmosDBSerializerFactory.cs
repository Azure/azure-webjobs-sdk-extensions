// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Core.Serialization;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class DefaultCosmosDBSerializerFactory : ICosmosDBSerializerFactory
    {
        public CosmosSerializer CreateSerializer() => null;

        public CosmosSerializer CreateSerializer(CosmosDBOptions cosmosDBOptions)
        {
            if (cosmosDBOptions.SerializerSettings.DateTimeParse == DateTimeHandling.None)
            {
                return new ObjectCosmosSerializer(new NewtonsoftJsonObjectSerializer(new JsonSerializerSettings()
                {
                    DateParseHandling = DateParseHandling.None,
                }));
            }
            else
            {
                return CreateSerializer();
            }
        }
    }
}
