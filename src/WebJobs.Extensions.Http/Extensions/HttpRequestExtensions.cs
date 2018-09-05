// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    public static class HttpRequestExtensions
    {
        private const int DefaultBufferSize = 1024;
        private const string EasyAuthIdentityHeader = "x-ms-client-principal";

        // Shared serializer instance. This is safe for multi-threaded use.
        private static readonly Lazy<DataContractJsonSerializer> ClaimsIdentitySerializer = new Lazy<DataContractJsonSerializer>(GetClaimsIdentitySerializer);

        public static async Task<string> ReadAsStringAsync(this HttpRequest request)
        {
            request.EnableRewind();

            string result = null;
            using (var reader = new StreamReader(
                request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: DefaultBufferSize,
                leaveOpen: true))
            {
                result = await reader.ReadToEndAsync();
            }

            request.Body.Seek(0, SeekOrigin.Begin);

            return result;
        }

        public static IDictionary<string, string> GetQueryParameterDictionary(this HttpRequest request)
        {
            // last one wins for any duplicate query parameters
            return request.Query.ToDictionary(p => p.Key, p => p.Value.Last());
        }

        public static ClaimsIdentity GetAppServiceIdentity(this HttpRequest request)
        {
            if (!request.Headers.ContainsKey(EasyAuthIdentityHeader))
            {
                return null;
            }
            string headerValue = request.Headers[EasyAuthIdentityHeader].First();
            return FromBase64EncodedJson(headerValue);
        }
      
        private static ClaimsIdentity FromBase64EncodedJson(string payload)
        {
            using (var buffer = new MemoryStream(Convert.FromBase64String(payload)))
            {
                ClaimsIdentitySlim slim = (ClaimsIdentitySlim)ClaimsIdentitySerializer.Value.ReadObject(buffer);
                return slim.ToClaimsIdentity();
            }
        }

        private static DataContractJsonSerializer GetClaimsIdentitySerializer()
        {
            // This serializer has the exact same settings as used by EasyAuth to ensure compatibility
            var settings = new DataContractJsonSerializerSettings();
            settings.UseSimpleDictionaryFormat = true;
            settings.DateTimeFormat = new DateTimeFormat("o");
            return new DataContractJsonSerializer(typeof(ClaimsIdentitySlim), settings);
        }
    }
}
