// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    internal static class HttpRequestMessageExtensions
    {
        public static IDictionary<string, string> GetRawHeaders(this HttpRequestMessage request)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var allHeadersRaw = request.Headers.ToString() + Environment.NewLine + request.Content?.Headers?.ToString();
            var rawHeaderLines = allHeadersRaw.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var header in rawHeaderLines)
            {
                int idx = header.IndexOf(':');
                string name = header.Substring(0, idx);
                string value = header.Substring(idx + 1).Trim();
                headers.Add(name, value);
            }

            return headers;
        }

        public static IDictionary<string, string> GetQueryParameterDictionary(this HttpRequestMessage request)
        {
            var keyValuePairs = request.GetQueryNameValuePairs();

            // last one wins for any duplicate query parameters
            return keyValuePairs.GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, s => s.Last().Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}
