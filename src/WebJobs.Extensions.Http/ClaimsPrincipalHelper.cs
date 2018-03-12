// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;
    using System.Security.Claims;

    internal static class ClaimsPrincipalHelper
    {
        // Shared serializer instance. This is safe for multi-threaded use.
        private static readonly DataContractJsonSerializer Serializer = GetCustomSerializer();

        public static ClaimsPrincipal FromBindingData(IReadOnlyDictionary<string, object> bindingData)
        {
            var req = bindingData.Values.FirstOrDefault(val => val.GetType() == typeof(HttpRequestMessage)) as HttpRequestMessage;
            if (req == null)
            {
                return new ClaimsPrincipal();
            }

            var claimsPrincipalHeaderValue = GetClaimsPrincipalHeaderValue(req);
            if (claimsPrincipalHeaderValue == null)
            {
                return new ClaimsPrincipal();
            }
            return FromBase64EncodedJson(claimsPrincipalHeaderValue);
        }

        private static ClaimsPrincipal FromBase64EncodedJson(string payload)
        {
            using (var buffer = new MemoryStream(Convert.FromBase64String(payload)))
            {
                ClaimsIdentitySlim slim = (ClaimsIdentitySlim)Serializer.ReadObject(buffer);
                return new ClaimsPrincipal(slim.ToClaimsIdentity());
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


        private static string GetClaimsPrincipalHeaderValue(HttpRequestMessage request)
        {
            var claimsPrincipalHeaders = request.Headers.Where(header => string.Equals(header.Key, "x-ms-client-principal", StringComparison.OrdinalIgnoreCase));
            if (!claimsPrincipalHeaders.Any())
            {
                return null;
            }
            else
            {
                return claimsPrincipalHeaders.First().Value.First();
            }
        }
    }
}
