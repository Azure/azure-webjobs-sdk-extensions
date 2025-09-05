// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Extensions.Http.Tests
{
    public class HttpTestHelpers
    {
        public static HttpRequest CreateHttpRequest(string method, string uriString, IHeaderDictionary headers = null, string body = null)
        {
            DefaultHttpContext context = new();
            ServiceCollection services = new();
            IServiceProvider sp = services.BuildServiceProvider();
            context.RequestServices = sp;

            Uri uri = new(uriString);
            HttpRequest request = context.Request;
            IHttpRequestFeature requestFeature = request.HttpContext.Features.Get<IHttpRequestFeature>();
            requestFeature.Method = method;
            requestFeature.Scheme = uri.Scheme;
            requestFeature.PathBase = uri.Host;
            requestFeature.Path = uri.GetComponents(UriComponents.KeepDelimiter | UriComponents.Path, UriFormat.Unescaped);
            requestFeature.PathBase = "/";
            requestFeature.QueryString = uri.GetComponents(UriComponents.KeepDelimiter | UriComponents.Query, UriFormat.Unescaped);

            headers ??= new HeaderDictionary();

            if (!string.IsNullOrEmpty(uri.Host))
            {    
                headers.Host = uri.Host;
            }

            if (body != null)
            {
                requestFeature.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
                request.ContentLength = request.Body.Length;
                headers.ContentLength = request.Body.Length;
            }

            requestFeature.Headers = headers;

            return request;
        }
    }
}
