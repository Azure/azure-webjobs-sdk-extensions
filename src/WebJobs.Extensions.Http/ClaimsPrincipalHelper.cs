// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;
    using System.Security.Claims;

    /// <summary>
    /// Provides methods to grab the ClaimsPrincipal from an HTTP Request trigger
    /// </summary>
    public static class ClaimsIdentityHelper
    {
        // Shared serializer instance. This is safe for multi-threaded use.
        private static readonly DataContractJsonSerializer Serializer = GetCustomSerializer();

        private const string EasyAuthIdentity = "x-ms-client-principal";

        /// <summary>
        /// Retrieves a serialized ClaimsIdentity object from the http request of an HTTP Trigger
        /// </summary>
        /// <param name="request">The request message from the HTTP Trigger</param>
        /// <param name="identityHeaderName">The name of the header the identity is stored on.</param>
        /// <returns></returns>
        public static ClaimsIdentity GetIdentityFromHttpRequest(HttpRequestMessage request, string identityHeaderName)
        {
            if (request == null)
            {
                return null;
            }

            string claimsIdentityHeaderValue = GetClaimsIdentityHeaderValue(request, identityHeaderName);
            if (claimsIdentityHeaderValue == null)
            {
                return null;
            }
            return FromBase64EncodedJson(claimsIdentityHeaderValue);
        }

        /// <summary>
        /// Adds a serialized ClaimsIdentity object to a specified http header
        /// </summary>
        /// <param name="request">The HTTP request to add the header to</param>
        /// <param name="identity">The ClaimsIdentity to be serialized</param>
        /// <param name="headerName">The name of the header to add the serialized value to</param>
        public static void AddIdentityToHttpRequest(HttpRequestMessage request, ClaimsIdentity identity, string headerName)
        {
            if (request == null)
            {
                return;
            }

            ClaimsIdentitySlim identitySlim = ClaimsIdentitySlim.FromClaimsIdentity(identity);
            using (var stream = new MemoryStream())
            {
                Serializer.WriteObject(stream, identitySlim);
                string encodedValue = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Position);
                request.Headers.Add(headerName, encodedValue);
            }       
        }

        private static ClaimsIdentity FromBase64EncodedJson(string payload)
        {
            using (var buffer = new MemoryStream(Convert.FromBase64String(payload)))
            {
                ClaimsIdentitySlim slim = (ClaimsIdentitySlim)Serializer.ReadObject(buffer);
                return slim.ToClaimsIdentity();
            }
        }

        private static DataContractJsonSerializer GetCustomSerializer()
        {
            // These settings make DCJS mostly compatible with Json.NET defaults.
            var settings = new DataContractJsonSerializerSettings();
            settings.UseSimpleDictionaryFormat = true;

            // Use the ISO 8601 recommended format for internet timestamps (see http://tools.ietf.org/html/rfc3339).
            settings.DateTimeFormat = new DateTimeFormat("o");

            return new DataContractJsonSerializer(typeof(ClaimsIdentitySlim), settings);
        }

        private static string GetClaimsIdentityHeaderValue(HttpRequestMessage request, string headerName)
        {
            return request.Headers
                .Where(header => string.Equals(header.Key, headerName, StringComparison.OrdinalIgnoreCase))
                .Select(header => header.Value.First())
                .FirstOrDefault();
        }
    }
}
