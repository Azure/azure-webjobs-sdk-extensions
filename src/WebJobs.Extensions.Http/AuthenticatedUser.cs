// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// A class containing information about the current authenticated user.
    /// </summary>
    public class AuthenticatedUser
    {
        /// <summary>
        /// The JWT access token for the user
        /// </summary>
        [JsonProperty("access_token")]
        public string AccessToken { get; private set; }

        /// <summary>
        /// The datetime that the user's access token expires
        /// </summary>
        [JsonProperty("expires_on")]
        public DateTime ExpiresOn { get; private set; }

        /// <summary>
        /// The JWT token that represents the identity of the user.
        /// </summary>
        [JsonProperty("id_token")]
        public string IdToken { get; private set; }

        /// <summary>
        /// The string representation of the identity provider for the authenticated user.
        /// </summary>
        [JsonProperty("provider_name")]
        public string ProviderName { get; private set; }

        /// <summary>
        /// The datetime that the user's access token expires
        /// </summary>
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; private set; }

        [JsonProperty("user_claims")]
        internal List<Claim> _userClaims { get; set; }

        /// <summary>
        /// An array of claims that apply to the authenticated user.
        /// </summary>
        [JsonIgnoreAttribute]
        public IReadOnlyList<Claim> UserClaims
        {
            get
            {
                return this._userClaims.AsReadOnly();
            }
        } 

        internal static AuthenticatedUser DeserializeJson(string json)
        {
            return JsonConvert.DeserializeObject<List<AuthenticatedUser>>(json, new JsonConverter[] { new ClaimConverter() })[0];
        }

        private class ClaimConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Claim);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject jObject = JObject.Load(reader);
                return new Claim(jObject["typ"].Value<string>(), jObject["val"].Value<string>());
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
