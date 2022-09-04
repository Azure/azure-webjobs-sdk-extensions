// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Bindings
{
    internal class CosmosDBJArrayBuilder : IAsyncConverter<CosmosDBAttribute, JArray>
    {
        private CosmosDBEnumerableBuilder<JToken> _builder;

        public CosmosDBJArrayBuilder(CosmosDBExtensionConfigProvider configProvider)
        {
            _builder = new CosmosDBEnumerableBuilder<JToken>(configProvider);
        }

        public async Task<JArray> ConvertAsync(CosmosDBAttribute attribute, CancellationToken cancellationToken)
        {
            IEnumerable<JToken> results = await _builder.ConvertAsync(attribute, cancellationToken);
            return JArray.FromObject(results);
        }
    }
}
