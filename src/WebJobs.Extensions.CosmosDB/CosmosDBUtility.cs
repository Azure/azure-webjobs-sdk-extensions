// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Documents;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal static class CosmosDBUtility
    {
        internal static bool TryGetDocumentClientException(Exception originalEx, out DocumentClientException documentClientEx)
        {
            documentClientEx = originalEx as DocumentClientException;

            if (documentClientEx != null)
            {
                return true;
            }

            AggregateException ae = originalEx as AggregateException;
            if (ae == null)
            {
                return false;
            }

            documentClientEx = ae.InnerException as DocumentClientException;

            return documentClientEx != null;
        }
    }
}
