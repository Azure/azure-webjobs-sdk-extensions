// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class DocumentQueryResponse<T>
    {
        public DocumentQueryResponse()
        {
            Results = Enumerable.Empty<T>();
        }

        public IEnumerable<T> Results { get; set; }

        public string ResponseContinuation { get; set; }
    }
}
