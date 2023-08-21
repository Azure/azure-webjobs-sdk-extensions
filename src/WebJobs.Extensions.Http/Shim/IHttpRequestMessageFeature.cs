// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#if NET6_0_OR_GREATER

using System.Net.Http;

namespace Microsoft.Azure.WebJobs.Extensions.Http.Shim
{
    public interface IHttpRequestMessageFeature
    {
        HttpRequestMessage HttpRequestMessage { get; set; }
    }
}

#endif