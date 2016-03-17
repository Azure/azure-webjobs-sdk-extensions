// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Reflection;
using Microsoft.Azure.Documents;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    internal static class DocumentDBTestUtility
    {
        public static DocumentClientException CreateDocumentClientException(HttpStatusCode status)
        {
            var parameters = new object[] { null, null, status, null };
            return Activator.CreateInstance(typeof(DocumentClientException), BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null) as DocumentClientException;
        }
    }
}
