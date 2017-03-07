// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB.Bindings
{
    internal class DocumentDBJArrayBuilder : IAsyncConverter<DocumentDBAttribute, JArray>
    {
        private DocumentDBEnumerableBuilder<JObject> _builder;

        public DocumentDBJArrayBuilder(DocumentDBConfiguration config)
        {
            _builder = new DocumentDBEnumerableBuilder<JObject>(config);
        }

        public async Task<JArray> ConvertAsync(DocumentDBAttribute attribute, CancellationToken cancellationToken)
        {
            IEnumerable<JObject> results = await _builder.ConvertAsync(attribute, cancellationToken);
            return JArray.FromObject(results);
        }
    }
}
