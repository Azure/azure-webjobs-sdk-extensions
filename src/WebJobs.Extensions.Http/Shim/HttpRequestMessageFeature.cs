// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.WebJobs.Extensions.Http.Shim
{
    internal sealed class HttpRequestMessageFeature : IHttpRequestMessageFeature
    {
        private readonly HttpContext _httpContext;

        private HttpRequestMessage _httpRequestMessage;

        public HttpRequestMessageFeature(HttpContext httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException("httpContext");
            }

            _httpContext = httpContext;
        }

        public HttpRequestMessage HttpRequestMessage
        {
            get
            {
                if (_httpRequestMessage == null)
                {
                    _httpRequestMessage = CreateHttpRequestMessage(_httpContext);
                }

                return _httpRequestMessage;
            }

            set
            {
                _httpRequestMessage = value;
            }
        }

        private static HttpRequestMessage CreateHttpRequestMessage(HttpContext httpContext)
        {
            HttpRequest request = httpContext.Request;
            string requestUri = request.Scheme + "://" + request.Host + request.PathBase + request.Path + request.QueryString;
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(new HttpMethod(request.Method), requestUri);
            httpRequestMessage.Properties["HttpContext"] = httpContext;
            httpRequestMessage.Content = new StreamContent(request.Body);
            foreach (KeyValuePair<string, StringValues> header in request.Headers)
            {
                if (!httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value))
                {
                    httpRequestMessage.Content.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value);
                }
            }

            return httpRequestMessage;
        }
    }
}

#endif